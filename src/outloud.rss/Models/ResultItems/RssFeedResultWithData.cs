namespace Outloud.Rss.Models.ResultItems
{
    public class RssFeedResultWithData : RssFeedResult
    {
        public RssFeedResultWithData(RssFeed rssFeed) : base(rssFeed)
        {
        }

        public List<RssFeedResultWithDataItem> ItemDatas { get; } = new();
    }
}
