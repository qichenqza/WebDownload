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
                int htmlLoadTimeout = 3 * 1000;
                // async load html from web url
                CancellationTokenSource cts = new();
                var htmlLoadTask = htmlWeb.LoadFromWebAsync(textBox1.Text, cts.Token);

                // async wait html load task, cancel task when timeout
                if (await Task.WhenAny(htmlLoadTask, Task.Delay(htmlLoadTimeout)) != htmlLoadTask)
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


        public static async Task DownloadFileAsync(HttpClient client, string url, FileStream fileStream, CancellationToken token)
        {
            if (string.IsNullOrEmpty(url)) { return; }

            using var httpStream = await client.GetStreamAsync(url, token);
            await httpStream.CopyToAsync(fileStream, token);
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
                int httpTimeout = 20 * 1000;
                string urlPrefix = @"http://data.gdeltproject.org/events";
                HttpClient httpClient = new() { Timeout = TimeSpan.FromMilliseconds(httpTimeout) };

                // copy checked file list into stack
                var checkedFileList = checkedListBox1.CheckedItems;
                int fileCount = checkedFileList.Count;
                // use a new list to store file name
                // when check list box is checked or unchecked the checked item list is changed
                var downloadFileList = new List<string>();
                for (int i = 0; i < fileCount; i++) downloadFileList.Add(checkedFileList[i]?.ToString() ?? "");

                int downloadFileCount = 0;
                // mark check list box to be uncheckable
                checkedListBox1.SelectionMode = SelectionMode.None;
                for (int i = 0; i < fileCount; i++)
                {
                    var fileName = downloadFileList[i];
                    ShowInfoInForm($"[INFO] Downloading files {i + 1}/{fileCount} ...");

                    // !!! FUCK C# using statement, may not dispose in a Task when exception occur
                    // 1. if file exists then continue
                    // 2. delete the bak file
                    // 3. create file stream of bak file
                    // 4. read stream of http client and write into file stream
                    // 5. dispose and close file stream
                    // 6. mv bak file to origin file and unzip if download success
                    // 7. download again if failed
                    if (string.IsNullOrEmpty(fileName)) { continue; }
                    var filePath = $"{folderPath}\\{fileName}";
                    if (File.Exists(filePath))
                    {
                        downloadFileCount++;
                        checkedListBox1.SetItemChecked(i, false);
                        continue;
                    }

                    for (int repeatCount = 0; repeatCount < AppUtil.MAX_RETRY; repeatCount++)
                    {
                        try { File.Delete($"{filePath}.bak"); } catch { }
                        using var fileStream = new FileStream($"{filePath}.bak", FileMode.CreateNew);

                        CancellationTokenSource cts = new();
                        var downloadTask = DownloadFileAsync(httpClient, $"{urlPrefix}/{fileName}", fileStream, cts.Token);
                        if (await Task.WhenAny(downloadTask, Task.Delay(httpTimeout)) != downloadTask) { cts.Cancel(); }
                        fileStream.Dispose(); fileStream.Close();

                        // success download file
                        if (downloadTask.Status == TaskStatus.RanToCompletion)
                        {
                            File.Move($"{filePath}.bak", filePath, true);
                            ZipFile.ExtractToDirectory(filePath, Path.GetDirectoryName(filePath) ?? "", true);

                            downloadFileCount++;
                            checkedListBox1.SetItemChecked(i, false);
                            break;
                        }
                        if (downloadTask.Exception != null) { ShowInfoInForm($"[ERROR] {downloadTask.Exception.Message}"); }
                        ShowInfoInForm($"[INFO] Retry downloading files {i + 1}/{fileCount} ...");
                    }
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

        //private static void MergeFileList(string[] inputFileList, string outputFilePath)
        //{
        //    if (File.Exists(outputFilePath)) { File.Move(outputFilePath, $"{outputFilePath}.bak", true); }

        //    using var outputStream = new FileStream(outputFilePath, FileMode.CreateNew);
        //    foreach (var inputFilePath in inputFileList)
        //    {
        //        using var inputStream = new FileStream(inputFilePath, FileMode.Open);
        //        inputStream.CopyTo(outputStream);
        //    }
        //}

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox3.DataBindings.Add("Text", this.notifyContext, "NotifyInfo");
        }

        private void ShowInfoInForm(string info)
        {
            DateTime now = DateTime.Now;
            info = $"[{now:yyyy-MM-dd HH:mm:ss}] {info}";
            this.notifyContext.NotifyInfo = info + "\r\n" + this.notifyContext.NotifyInfo;
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

                List<string> fileList = [];
                string filePat = @"^[0-9]{4}\S+$";
                var fileReg = new Regex(filePat);
                foreach (var filePath in Directory.GetFiles(folderPath, "*.csv"))
                {
                    var fileName = Path.GetFileName(filePath);
                    if (!fileReg.Match(fileName).Success) { continue; }
                    fileList.Add(filePath);
                }

                int fileProcessTimeout = 60 * 1000;
                CancellationTokenSource cts = new();
                var loadDataTask = AppUtil.LoadDataFromFile(folderPath, cts.Token);
                if (await Task.WhenAny(loadDataTask, Task.Delay(fileProcessTimeout)) != loadDataTask)
                {
                    cts.Cancel();
                    ShowInfoInForm($"[ERROR] Timeout while loading middle data from file.");
                }
                if (loadDataTask.Exception != null)
                {
                    AppUtil.ClearMiddleData(folderPath);
                    throw loadDataTask.Exception;
                }
                ShowInfoInForm($"[INFO] Finish loading middle data from file.");

                var cntDict = AppUtil.CntDict;
                var geoDict = AppUtil.GeoDict;
                var recordFiles = AppUtil.RecordedFileList;
                var csvReaderConfig = CsvConfiguration.FromAttributes<GDELTEvent>(CultureInfo.InvariantCulture);
                {
                    foreach (var filePath in fileList)
                    {
                        var fileName = Path.GetFileName(filePath);
                        string year = fileName[..4];
                        if (recordFiles.Contains(fileName))
                        {
                            continue;
                        }

                        ShowInfoInForm($"[INFO] Start to read records from {fileName} file.");

                        using var csvFileReader = new StreamReader($"{filePath}");
                        using var csvReader = new CsvReader(csvFileReader, csvReaderConfig);
                        var tmpCntDict = new Dictionary<string, GDELTEventResult>();
                        var tmpGeoDict = new Dictionary<string, GDELTEventResult>();

                        cts = new();
                        var readRecordsTask = Task.Run(async () =>
                        {
                            await foreach (var item in csvReader.GetRecordsAsync<GDELTEvent>())
                            {
                                // change item year to year in file
                                item.Year = year;
                                ProcessRecords(item, ref tmpCntDict, GDELTEventType.CNT, item.Year);
                                ProcessRecords(item, ref tmpGeoDict, GDELTEventType.GEO, item.Year);
                            }
                        }, cts.Token);

                        if (await Task.WhenAny(readRecordsTask, Task.Delay(fileProcessTimeout)) != readRecordsTask)
                        {
                            cts.Cancel();
                            throw new Exception($"[ERROR] Timeout while reading and processing {fileName} file.");
                        }
                        csvReader.Dispose();
                        csvFileReader.Dispose(); csvFileReader.Close();

                        if (readRecordsTask.Exception != null) { throw readRecordsTask.Exception; }

                        if (readRecordsTask.Status == TaskStatus.RanToCompletion)
                        {
                            // finish reading and processing
                            // update file records, cnt event result, geo event result
                            recordFiles.Add(fileName);
                            AppUtil.UpdateResultDict(ref cntDict, tmpCntDict);
                            AppUtil.UpdateResultDict(ref geoDict, tmpGeoDict);
                        }

                        ShowInfoInForm($"[INFO] Finish reading records from {fileName} file.");
                    }
                }

                cts = new();
                var saveDataTask = AppUtil.SaveDataIntoFile(folderPath, cts.Token);
                if (await Task.WhenAny(saveDataTask, Task.Delay(fileProcessTimeout)) != saveDataTask)
                {
                    cts.Cancel();
                    ShowInfoInForm($"[ERROR] Timeout while saving middle data info file.");
                }
                if (saveDataTask.Exception != null) { throw saveDataTask.Exception; }
                ShowInfoInForm($"[INFO] Finish saving middle data into file.");
            }
            catch (Exception error)
            {
                ShowInfoInForm($"[ERROR] {error.Message}");
                EnableButtons();
                return;
            }

            EnableButtons();
        }

        public async static Task SaveResult(string fileName, GDELTEventType type, CsvConfiguration cfg, CancellationToken token)
        {
            var resultList = new List<GDELTEventResult>();
            List<GDELTEventResult> originResultList;
            if (type == GDELTEventType.CNT)
            {
                originResultList = [.. AppUtil.CntDict.Values];
            }
            else
            {

                originResultList = [.. AppUtil.GeoDict.Values];
            }

            // as required the country pair should be recorded twice
            foreach (var item in originResultList)
            {
                resultList.Add(item);
                if (item.CountryCode1 != item.CountryCode2)
                {
                    var newItem = GDELTEventResult.DeepCopy(item);
                    if (newItem == null) { continue; }
                    newItem.CountryCode1 = item.CountryCode2;
                    newItem.CountryCode2 = item.CountryCode1;
                    resultList.Add(newItem);
                }
            }

            using var outputStream = new StreamWriter(fileName, false);
            using var csvWriter = new CsvWriter(outputStream, cfg);
            await csvWriter.WriteRecordsAsync(resultList, token);
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
                    int writeRecordsTimeout = 10 * 1000;
                    CancellationTokenSource cts = new();
                    var cntSaveTask = SaveResult($"{saveFilePath}.cnt.csv.bak", GDELTEventType.CNT, csvWriterConfig, cts.Token);
                    var geoSaveTask = SaveResult($"{saveFilePath}.geo.csv.bak", GDELTEventType.GEO, csvWriterConfig, cts.Token);

                    var taskDeny = Task.Delay(writeRecordsTimeout);
                    if (await Task.WhenAny(Task.WhenAll(cntSaveTask, geoSaveTask), taskDeny) == taskDeny)
                    {
                        cts.Cancel();
                        throw new Exception($"[ERROR] Timeout while save process result into {saveFilePath} file.");
                    }

                    if (cntSaveTask.Exception != null) { throw cntSaveTask.Exception; }
                    if (geoSaveTask.Exception != null) { throw geoSaveTask.Exception; }

                    if (cntSaveTask.Status == TaskStatus.RanToCompletion && geoSaveTask.Status == TaskStatus.RanToCompletion)
                    {
                        if (File.Exists($"{saveFilePath}.cnt.csv.bak")) { File.Move($"{saveFilePath}.cnt.csv.bak", $"{saveFilePath}.cnt.csv", true); }
                        if (File.Exists($"{saveFilePath}.geo.csv.bak")) { File.Move($"{saveFilePath}.geo.csv.bak", $"{saveFilePath}.geo.csv", true); }
                    }
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
