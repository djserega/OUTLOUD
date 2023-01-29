using System.ComponentModel.DataAnnotations;

namespace Outloud.Rss.Models
{
    public class RssFeed
    {
        [Key]
        public uint Id { get; set; }
        public Uri? Uri { get; set; }
        public string? Title { get; set; }
        public bool IsActive { get; set; }
        public DateTime UnreadDate { get; set; }
        public List<RssFeedItemData> ItemDatas { get; } = new();
    }
}
