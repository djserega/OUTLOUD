namespace Outloud.Rss.Models.ResultItems
{
    public class RssFeedResult
    {
        public RssFeedResult(RssFeed rssFeed)
        {
            Uri = rssFeed.Uri;
            Title = rssFeed.Title;
            IsActive = rssFeed.IsActive;
        }

        public Uri? Uri { get; set; }
        public string? Title { get; set; }
        public bool IsActive { get; set; }
    }
}
