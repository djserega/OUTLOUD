namespace Outloud.Rss.Models.ResultItems
{
    public class TimePeriodDownloadingNews
    {
        public TimePeriodDownloadingNews(uint hour, uint minute, uint second)
        {
            Hour = hour;
            Minute = minute;
            Second = second;
        }

        public uint Hour { get; set; }
        public uint Minute { get; set; }
        public uint Second { get; set; }
    }
}
