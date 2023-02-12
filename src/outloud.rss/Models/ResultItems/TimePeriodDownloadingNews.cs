namespace Outloud.Rss.Models.ResultItems
{
    public class TimePeriodDownloadingNews
    {
        public TimePeriodDownloadingNews(int hour, int minute, int second)
        {
            Hour = hour;
            Minute = minute;
            Second = second;
        }

        public int Hour { get; set; }
        public int Minute { get; set; }
        public int Second { get; set; }
    }
}
