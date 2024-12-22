using CsvHelper.Configuration.Attributes;

namespace WebSiteDownload
{
    public enum GDELTEventType
    {
        CNT,
        GEO
    }

    //[Delimiter(",")]
    [Delimiter("\t")]
    [HasHeaderRecord(false)]
    [IgnoreBlankLines(true)]
    public class GDELTEvent()
    {
        private string _cntHash = "";
        private string _geoHash = "";

        [Index(3)]
        public string Year { get; set; } = "";
        [Index(7)]
        public string Actor1CountryCode { get; set; } = "";
        [Index(17)]
        public string Actor2CountryCode { get; set; } = "";
        [Index(37)]
        public string Actor1Geo_CountryCode { get; set; } = "";
        [Index(44)]
        public string Actor2Geo_CountryCode { get; set; } = "";
        [Index(30)]
        public string GoldsteinScale { get; set; } = "";
        [Index(56)]
        public string DateAdded { get; set; } = "";

        public string GetHashCode(GDELTEventType eventType)
        {
            switch (eventType)
            {
                case GDELTEventType.CNT:
                    if (string.IsNullOrEmpty(_cntHash))
                    {
                        if (Actor1CountryCode == Actor2CountryCode) { _cntHash = AppUtil.CalculateSetHash(new List<string>() { Year, Actor1CountryCode }); }
                        else { _cntHash = AppUtil.CalculateSetHash(new List<string>() { Year, Actor1CountryCode, Actor2CountryCode }); }
                    }
                    return _cntHash;
                case GDELTEventType.GEO:
                    if (string.IsNullOrEmpty(_geoHash))
                    {
                        if (Actor1Geo_CountryCode == Actor2Geo_CountryCode) { _geoHash = AppUtil.CalculateSetHash(new List<string>() { Year, Actor1Geo_CountryCode }); }
                        else { _geoHash = AppUtil.CalculateSetHash(new List<string>() { Year, Actor1Geo_CountryCode, Actor2Geo_CountryCode }); }
                    }
                    return _geoHash;
                default:
                    return "";
            }
        }
    }
}
