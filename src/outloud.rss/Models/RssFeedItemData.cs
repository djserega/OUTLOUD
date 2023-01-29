using System.ComponentModel.DataAnnotations;

namespace Outloud.Rss.Models
{
    public class RssFeedItemData
    {
        [Key]
        public uint Id { get; set; }
        public string? Title { get; set; }
        public string? Links { get; set; }
        public string? Description { get; set; }
        public DateTimeOffset? DatePublication { get; set; }
        public bool IsRead { get; set; }

        public override bool Equals(object? obj)
        {
            return obj is RssFeedItemData data &&
                   Title == data.Title &&
                   Links == data.Links &&
                   Description == data.Description &&
                   EqualityComparer<DateTimeOffset?>.Default.Equals(DatePublication, data.DatePublication);
        }
    }
}
