using CsvHelper;
using CsvHelper.Configuration;
using HtmlAgilityPack;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;


namespace WebSiteDownload
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public NotifyContext notifyContext = new();
        public HtmlWeb htmlWeb = new();

        private async void button1_Click(object sender, EventArgs e)
        {
            // disable button after button is clicked
            DisableButtons();

            // clear notify context which connected to other components
            ClearForm();

            try
            {
                // async load html from web url
                CancellationTokenSource cts = new();
                var htmlLoadTask = htmlWeb.LoadFromWebAsync(textBox1.Text, cts.Token);

                // async wait html load task, cancel task when timeout
                if (await Task.WhenAny(htmlLoadTask, Task.Delay(3000)) != htmlLoadTask)
                {
                    // timeout logic
                    cts.Cancel();
                    throw new Exception($"[ERROR] Timeout while loading html from {textBox1.Text}.");
                }

                if (htmlLoadTask.Exception != null) { throw htmlLoadTask.Exception; }

                // get node list from html parser
                var doc = htmlLoadTask.Result;
                var nodes = doc?.DocumentNode.SelectNodes(textBox2.Text) ?? throw new Exception($"[ERROR] Found on data for given XPath {textBox2.Text}.");


                // check filename whether it matchs given regex or not
                var fileNameList = new ArrayList();
                string filePat = @"^[0-9]+\S+\.zip$";
                var fileReg = new Regex(filePat);

                foreach (var node in nodes)
                {
                    var fileName = node?.GetAttributeValue("href", "") ?? "";
                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = node?.GetAttributeValue("src", "") ?? "";
                        if (string.IsNullOrEmpty(fileName)) { continue; }
                    }

                    if (fileReg.Match(fileName).Success) { fileNameList.Add(fileName); }
                }

                // update checked list box ui
                checkedListBox1.BeginUpdate();
                checkedListBox1.Items.Clear();
                for (int i = 0; i < fileNameList.Count; i++)
                {
                    var fileName = fileNameList[i]?.ToString();
                    if (string.IsNullOrEmpty(fileName)) { continue; }
                    checkedListBox1.Items.Add(fileName, true);
                }
                checkedListBox1.EndUpdate();
            }
            catch (Exception error)
            {
                ShowInfoInForm($"[ERROR] {error.Message}");
                EnableButtons();
                return;
            }

            ShowInfoInForm($"[INFO] Parsing html success, found {checkedListBox1.Items.Count} files.");
            EnableButtons();
        }


        public static async Task DownloadFileAsync(HttpClient client, String url, string filePath, CancellationToken token)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(filePath)) { return; }

            if (!File.Exists(filePath))
            {
                using var httpStream = await client.GetStreamAsync(url, token);
                var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token).Token;
                using var fileStream = new FileStream(filePath, FileMode.CreateNew);
                await httpStream.CopyToAsync(fileStream, linkedToken);
            }
            ZipFile.ExtractToDirectory(filePath, Path.GetDirectoryName(filePath) ?? "", true);
        }

        private void DisableButtons()
        {
            button1.Enabled = false;
            button2.Enabled = false;
            button3.Enabled = false;
            button4.Enabled = false;
        }
        private void EnableButtons()
        {
            Application.DoEvents();
            button1.Enabled = true;
            button2.Enabled = true;
            button3.Enabled = true;
            button4.Enabled = true;
        }


        private async void button2_Click(object sender, EventArgs e)
        {
            DisableButtons();

            try
            {
                // open folder browser dialog, select location of download files
                var folderDialog = new FolderBrowserDialog();
                string folderPath = "";
                if (folderDialog.ShowDialog() == DialogResult.OK) { folderPath = folderDialog.SelectedPath; }
                if (string.IsNullOrEmpty(folderPath)) { throw new Exception("[ERROR] Invalid selected folder path."); }

                // init http client
                int httpTimeout = 10000;
                string urlPrefix = @"http://data.gdeltproject.org/events";
                HttpClient httpClient = new() { Timeout = TimeSpan.FromMilliseconds(httpTimeout) };

                // copy checked file list into stack
                int downloadFileCount = 0;
                var checkedFileList = checkedListBox1.CheckedItems;
                var downloadFileStack = new Stack<(int, string)>();
                int fileCount = checkedFileList.Count;
                for (int i = fileCount - 1; i >= 0; i--) downloadFileStack.Push((i, checkedFileList[i]?.ToString() ?? ""));

                // loop pop stack, download file into local terminal
                int maxRepeatCount = fileCount;
                // mark check list box to be uncheckable
                checkedListBox1.SelectionMode = SelectionMode.None;
                while (downloadFileStack.TryPop(out (int, string) downloadItem))
                {
                    if (maxRepeatCount <= 0) { break; }

                    var index = downloadItem.Item1;
                    var fileName = downloadItem.Item2;
                    if (string.IsNullOrEmpty(fileName)) { continue; }

                    ShowInfoInForm($"[INFO] Downloading files {index + 1}/{fileCount} ...");
                    CancellationTokenSource cts = new();
                    var downloadTask = DownloadFileAsync(httpClient, $"{urlPrefix}/{fileName}", $"{folderPath}\\{fileName}", cts.Token);
                    if (await Task.WhenAny(downloadTask, Task.Delay(httpTimeout)) != downloadTask) { cts.Cancel(); }

                    // success download file
                    if (downloadTask.Status == TaskStatus.RanToCompletion)
                    {
                        downloadFileCount++;
                        checkedListBox1.SetItemChecked(index, false);
                        continue;
                    }

                    // something wrong, repeat downloading
                    downloadFileStack.Push(downloadItem);
                    maxRepeatCount--;
                }
                ShowInfoInForm($"[INFO] Finish downloading files {downloadFileCount}/{fileCount}.");
            }
            catch (Exception error)
            {
                ShowInfoInForm($"[ERROR] {error.Message}");
                EnableButtons();
                return;
            }

            // reset check list box
            checkedListBox1.SelectionMode = SelectionMode.One;
            EnableButtons();
        }

        private static void ProcessRecords(GDELTEvent gDELTEvent, ref Dictionary<string, GDELTEventResult> resultDict, GDELTEventType eventType, string year)
        {
            string cntCode1 = "", cntCode2 = "";
            if (eventType == GDELTEventType.CNT)
            {
                cntCode1 = gDELTEvent.Actor1CountryCode;
                cntCode2 = gDELTEvent.Actor2CountryCode;
            }
            if (eventType == GDELTEventType.GEO)
            {

                cntCode1 = gDELTEvent.Actor1Geo_CountryCode;
                cntCode2 = gDELTEvent.Actor2Geo_CountryCode;
            }
            // continue if any country code is empty
            if (string.IsNullOrEmpty(cntCode1) || string.IsNullOrEmpty(cntCode2)) { return; }

            // get gdelt event result object and handle current gdelt event item
            var hashCode = gDELTEvent.GetHashCode(eventType);
            if (resultDict.TryGetValue(hashCode, out GDELTEventResult? eventResult))
            {
                eventResult.ProcessGDELTEvent(gDELTEvent);
            }
            else
            {
                GDELTEventResult result = new() { Year = year, CountryCode1 = cntCode1, CountryCode2 = cntCode2 };
                result.ProcessGDELTEvent(gDELTEvent);
                resultDict.Add(hashCode, result);
            }
        }

        private static void MergeFileList(string[] inputFileList, string outputFilePath)
        {
            if (File.Exists(outputFilePath)) { File.Move(outputFilePath, $"{outputFilePath}.bak", true); }

            using var outputStream = new FileStream(outputFilePath, FileMode.CreateNew);
            foreach (var inputFilePath in inputFileList)
            {
                using var inputStream = new FileStream(inputFilePath, FileMode.Open);
                inputStream.CopyTo(outputStream);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox3.DataBindings.Add("Text", this.notifyContext, "NotifyInfo");
        }

        private void ShowInfoInForm(string info)
        {
            if (!string.IsNullOrEmpty(this.notifyContext.NotifyInfo)) { this.notifyContext.NotifyInfo += "\r\n"; }
            this.notifyContext.NotifyInfo += info;
        }
        private void ClearForm()
        {
            this.notifyContext.Clear();
            checkedListBox1.Items.Clear();
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            DisableButtons();

            try
            {
                // open folder browser dialog, select location of download files
                var folderDialog = new FolderBrowserDialog();
                string folderPath = "";
                if (folderDialog.ShowDialog() == DialogResult.OK) { folderPath = folderDialog.SelectedPath; }
                if (string.IsNullOrEmpty(folderPath)) { throw new Exception("[ERROR] Invalid selected folder path."); }

                // start to go through csv files and merge them
                Dictionary<string, ArrayList> yearFileListDict = [];
                string filePat = @"^[0-9]+\S+$";
                var fileReg = new Regex(filePat);
                foreach (var filePath in Directory.GetFiles(folderPath, "*.csv"))
                {
                    var fileName = Path.GetFileName(filePath).Split(".")[0];
                    if (fileName.Length <= 4 || !fileReg.Match(fileName).Success) { continue; }
                    string yearStr = fileName[0..4];
                    if (yearFileListDict.TryGetValue(yearStr, out ArrayList? fileList)) { fileList.Add(filePath); }
                    else yearFileListDict.Add(yearStr, [filePath]);
                }

                // merge files
                foreach (KeyValuePair<string, ArrayList> item in yearFileListDict)
                {
                    var fileList = item.Value.ToArray(typeof(string));
                    ShowInfoInForm($"[INFO] Merge {fileList.Length} files into {item.Key}.csv file");
                    MergeFileList((string[])fileList, $"{folderPath}\\{item.Key}.csv");
                }

                // handle tsv file
                var cntDict = AppUtil.GetGDELTEventResultDict(GDELTEventType.CNT);
                var geoDict = AppUtil.GetGDELTEventResultDict(GDELTEventType.GEO);
                var csvReaderConfig = CsvConfiguration.FromAttributes<GDELTEvent>(CultureInfo.InvariantCulture);
                {
                    foreach (var year in yearFileListDict.Keys)
                    {
                        ShowInfoInForm($"[INFO] Start to read records from {year}.csv file.");

                        using var csvFileReader = new StreamReader($"{folderPath}\\{year}.csv");
                        using var csvReader = new CsvReader(csvFileReader, csvReaderConfig);

                        int readRecordsTimeout = 60000;
                        CancellationTokenSource cts = new();
                        var readRecordsTask = Task.Run(async () =>
                        {
                            await foreach (var item in csvReader.GetRecordsAsync<GDELTEvent>())
                            {
                                ProcessRecords(item, ref cntDict, GDELTEventType.CNT, item.Year);
                                ProcessRecords(item, ref geoDict, GDELTEventType.GEO, item.Year);
                            }
                        }, cts.Token);

                        if (await Task.WhenAny(readRecordsTask, Task.Delay(readRecordsTimeout)) != readRecordsTask)
                        {
                            cts.Cancel();
                            throw new Exception($"[ERROR] Timeout while reading and processing {year}.csv file.");
                        }

                        if (readRecordsTask.Exception != null) { throw readRecordsTask.Exception; }

                        ShowInfoInForm($"[INFO] Finish reading records from {year}.csv file.");
                    }
                }
            }
            catch (Exception error)
            {
                ShowInfoInForm($"[ERROR] {error.Message}");
                EnableButtons();
                return;
            }

            EnableButtons();
        }

        private async void button4_Click(object sender, EventArgs e)
        {
            DisableButtons();

            try
            {
                // open file dialog choose file path to save results
                string saveFilePath = "";
                var saveFileDialog = new SaveFileDialog();
                if (saveFileDialog.ShowDialog() == DialogResult.OK) { saveFilePath = saveFileDialog.FileName; }
                if (string.IsNullOrEmpty(saveFilePath)) { throw new Exception("[ERROR] Invalid selected file path."); }

                // get cfg from class annotation
                var csvWriterConfig = CsvConfiguration.FromAttributes<GDELTEventResult>(CultureInfo.InvariantCulture);
                {
                    var cntResultList = new List<GDELTEventResult>();
                    var geoResultList = new List<GDELTEventResult>();

                    // as required the country pair should be recorded twice
                    foreach (var item in AppUtil.GetGDELTEventResultDict(GDELTEventType.CNT).Values)
                    {
                        cntResultList.Add(item);
                        if (item.CountryCode1 != item.CountryCode2)
                        {
                            var newItem = GDELTEventResult.DeepCopy(item);
                            if (newItem == null) { continue; }
                            newItem.CountryCode1 = item.CountryCode2;
                            newItem.CountryCode2 = item.CountryCode1;
                            cntResultList.Add(newItem);
                        }
                    }
                    foreach (var item in AppUtil.GetGDELTEventResultDict(GDELTEventType.GEO).Values)
                    {
                        geoResultList.Add(item);
                        if (item.CountryCode1 != item.CountryCode2)
                        {
                            var newItem = GDELTEventResult.DeepCopy(item);
                            if (newItem == null) { continue; }
                            newItem.CountryCode1 = item.CountryCode2;
                            newItem.CountryCode2 = item.CountryCode1;
                            geoResultList.Add(newItem);
                        }
                    }

                    int writeRecordsTimeout = 10000;
                    CancellationTokenSource cts = new();
                    var csvWriteCntTask = WriteRecordsAsync($"{saveFilePath}.cnt.csv", cntResultList, csvWriterConfig, cts.Token);
                    var csvWriteGeoTask = WriteRecordsAsync($"{saveFilePath}.geo.csv", geoResultList, csvWriterConfig, cts.Token);

                    var taskDeny = Task.Delay(writeRecordsTimeout);
                    if (await Task.WhenAny(csvWriteCntTask, csvWriteGeoTask, taskDeny) == taskDeny)
                    {
                        cts.Cancel();
                        throw new Exception($"[ERROR] Timeout while save process result into {saveFilePath} file.");
                    }

                    if (csvWriteCntTask.Exception != null) { throw csvWriteCntTask.Exception; }
                    if (csvWriteGeoTask.Exception != null) { throw csvWriteGeoTask.Exception; }
                }
                ShowInfoInForm($"[INFO] Process result saved to {saveFilePath} success.");
            }
            catch (Exception error)
            {
                ShowInfoInForm($"[ERROR] {error.Message}");
                EnableButtons();
                return;
            }

            EnableButtons();
        }
        private static async Task WriteRecordsAsync(string filePath, List<GDELTEventResult> resultList, CsvConfiguration cfg, CancellationToken token)
        {
            if (File.Exists(filePath)) { File.Move(filePath, $"{filePath}.bak", true); }
            using var outputStream = new StreamWriter(filePath);
            using var csvWriter = new CsvWriter(outputStream, cfg);
            await csvWriter.WriteRecordsAsync(resultList, token);
        }
    }


    public abstract class BindableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            var handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) { return false; }
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class NotifyContext : BindableObject
    {
        private string _notifyInfo = "";
        public string NotifyInfo
        {
            get { return _notifyInfo; }
            set { SetProperty(ref _notifyInfo, value); }
        }

        public void Clear()
        {
            this.NotifyInfo = "";
        }
    }
}
