namespace Outloud.Rss.Models.ResultItems
{
    public class RssFeedResultWithDataItem
    {
        public RssFeedResultWithDataItem(RssFeedItemData itemData)
        {
            Title = itemData.Title;
            Links = itemData.Links;
            Description = itemData.Description;
            DatePublication = itemData.DatePublication;
            IsRead = itemData.IsRead;
        }

        public string? Title { get; set; }
        public string? Links { get; set; }
        public string? Description { get; set; }
        public DateTimeOffset? DatePublication { get; set; }
        public bool IsRead { get; set; }

        public override bool Equals(object? obj)
        {
            return obj is RssFeedResultWithDataItem data &&
                   Title == data.Title &&
                   Links == data.Links &&
                   Description == data.Description &&
                   EqualityComparer<DateTimeOffset?>.Default.Equals(DatePublication, data.DatePublication);
        }
    }
}
