using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WebSiteDownload
{
    public class AppUtil
    {
        private static readonly Guid guid = Guid.NewGuid();
        public static readonly string cntDictName = ".cnt.dict.data";
        public static readonly string geoDictName = ".geo.dict.data";
        public static readonly string recordedName = ".file.list.data";
        public static readonly int MAX_RETRY = 3;

        public static Dictionary<string, GDELTEventResult> CntDict { get; set; } = [];
        public static Dictionary<string, GDELTEventResult> GeoDict { get; set; } = [];
        public static HashSet<string> RecordedFileList { get; set; } = [];

        public static void ClearMiddleData(string folderPath)
        {
            File.Delete($"{folderPath}/{cntDictName}");
            File.Delete($"{folderPath}/{geoDictName}");
            File.Delete($"{folderPath}/{recordedName}");
        }

        public static void UpdateResultDict(ref Dictionary<string, GDELTEventResult> dstDict, Dictionary<string, GDELTEventResult> tmpDict)
        {
            foreach (var key in tmpDict.Keys)
            {
                if (dstDict.TryGetValue(key, out GDELTEventResult? dstItem))
                {
                    dstItem.ScaleSum += tmpDict[key].ScaleSum;
                    dstItem.PositiveScaleSum += tmpDict[key].PositiveScaleSum;
                    dstItem.NegativeScaleSum += tmpDict[key].NegativeScaleSum;
                    dstItem.MorePositiveScaleSum += tmpDict[key].MorePositiveScaleSum;
                    dstItem.MoreNegativeScaleSum += tmpDict[key].MoreNegativeScaleSum;
                    dstItem.ScaleCount += tmpDict[key].ScaleCount;
                    dstItem.PositiveScaleCount += tmpDict[key].PositiveScaleCount;
                    dstItem.NegativeScaleCount += tmpDict[key].NegativeScaleCount;
                    dstItem.NeutralScaleCount += tmpDict[key].NeutralScaleCount;
                    dstItem.MorePositiveScaleCount += tmpDict[key].MorePositiveScaleCount;
                    dstItem.MoreNegativeScaleCount += tmpDict[key].MoreNegativeScaleCount;
                }
                else
                {
                    dstDict.Add(key, tmpDict[key]);
                }
            }
        }

        public static async Task SaveDataIntoFile(string folderName, CancellationToken token)
        {
            var cntDictPath = $"{folderName}/{cntDictName}";
            var geoDictPath = $"{folderName}/{geoDictName}";
            var recordFilePath = $"{folderName}/{recordedName}";

            var cntTask = File.WriteAllTextAsync($"{cntDictPath}.bak", JsonSerializer.Serialize(CntDict), token);
            var geoTask = File.WriteAllTextAsync($"{geoDictPath}.bak", JsonSerializer.Serialize(GeoDict), token);
            var fileNameTask = File.WriteAllTextAsync($"{recordFilePath}.bak", JsonSerializer.Serialize(RecordedFileList), token);

            await Task.WhenAll(cntTask, geoTask, fileNameTask).ContinueWith(_ =>
            {
                if (File.Exists($"{cntDictPath}.bak"))
                {
                    File.Move($"{cntDictPath}.bak", cntDictPath, true);
                }
                if (File.Exists($"{geoDictPath}.bak"))
                {
                    File.Move($"{geoDictPath}.bak", geoDictPath, true);
                }
                if (File.Exists($"{recordFilePath}.bak"))
                {
                    File.Move($"{recordFilePath}.bak", recordFilePath, true);
                }
            }, token);
        }

        public static async Task LoadDataFromFile(string folderName, CancellationToken token)
        {
            await Task.Run(() =>
            {
                var cntDictPath = $"{folderName}/{cntDictName}";
                var geoDictPath = $"{folderName}/{geoDictName}";
                var recordFilePath = $"{folderName}/{recordedName}";

                if (File.Exists(cntDictPath))
                {
                    CntDict = JsonSerializer.Deserialize<Dictionary<string, GDELTEventResult>>(File.ReadAllText(cntDictPath)) ?? [];
                }
                if (File.Exists(geoDictPath))
                {
                    GeoDict = JsonSerializer.Deserialize<Dictionary<string, GDELTEventResult>>(File.ReadAllText(geoDictPath)) ?? [];
                }
                if (File.Exists(recordFilePath))
                {
                    RecordedFileList = JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(recordFilePath)) ?? [];
                }
            }, token);
        }

        public static string CalculateSetHash(ICollection set)
        {
            var combinedHash = SHA256.HashData(guid.ToByteArray());
            foreach (var item in set)
            {
                byte[] itemBytes = Encoding.UTF8.GetBytes(item.ToString() ?? "");
                byte[] itemHash = SHA256.HashData(itemBytes);

                if (itemHash == null || combinedHash == null || combinedHash.Length != itemHash.Length) { continue; }

                combinedHash = XorByteArrays(combinedHash, itemHash, combinedHash.Length);
            }

            return BitConverter.ToString(combinedHash ?? []).Replace("-", "").ToLower();
        }

        private static byte[] XorByteArrays(byte[] arr1, byte[] arr2, int len)
        {
            byte[] result = new byte[len];
            for (int i = 0; i < len; i++) { result[i] = (byte)(arr1[i] ^ arr2[i]); }
            return result;
        }
    }
}

