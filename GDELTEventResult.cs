using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json;

namespace WebSiteDownload
{
    [Delimiter(",")]
    public class GDELTEventResult()
    {
        public string Year { get; set; } = "";
        public string CountryCode1 { get; set; } = "";
        public string CountryCode2 { get; set; } = "";
        public double ScaleSum { get; set; } = 0;
        public double PositiveScaleSum { get; set; } = 0;
        public double NegativeScaleSum { get; set; } = 0;
        public double MorePositiveScaleSum { get; set; } = 0;
        public double MoreNegativeScaleSum { get; set; } = 0;
        public int ScaleCount { get; set; } = 0;
        public int PositiveScaleCount { get; set; } = 0;
        public int NegativeScaleCount { get; set; } = 0;
        public int NeutralScaleCount { get; set; } = 0;
        public int MorePositiveScaleCount { get; set; } = 0;
        public int MoreNegativeScaleCount { get; set; } = 0;

        public static GDELTEventResult? DeepCopy(GDELTEventResult result)
        {
            return JsonConvert.DeserializeObject<GDELTEventResult>(JsonConvert.SerializeObject(result));
        }

        public void ProcessGDELTEvent(GDELTEvent item)
        {
            double scale;
            try { scale = Convert.ToDouble(item.GoldsteinScale); }
            catch (Exception) { return; }

            this.ScaleCount++;
            this.ScaleSum += scale;

            if (scale > 0)
            {
                this.PositiveScaleCount++;
                this.PositiveScaleSum += scale;
                if (scale > 5.2)
                {
                    this.MorePositiveScaleCount++;
                    this.MorePositiveScaleSum += scale;
                }

            }
            else if (scale < 0)
            {
                this.NegativeScaleCount++;
                this.NegativeScaleSum += scale;
                if (scale < -2.2)
                {
                    this.MoreNegativeScaleCount++;
                    this.MoreNegativeScaleSum += scale;
                }
            }
            else
            {
                this.NeutralScaleCount++;
            }
        }
    }
}
