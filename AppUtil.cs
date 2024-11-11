using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace WebSiteDownload
{
    public class AppUtil
    {
        private static readonly Guid guid = Guid.NewGuid();
        private static readonly Dictionary<string, GDELTEventResult> cntDict = [];
        private static readonly Dictionary<string, GDELTEventResult> geoDict = [];

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

        public static Dictionary<string, GDELTEventResult> GetGDELTEventResultDict(GDELTEventType eventType)
        {
            if (eventType == GDELTEventType.CNT) { return cntDict; }
            else { return geoDict; }
        }
    }
}

