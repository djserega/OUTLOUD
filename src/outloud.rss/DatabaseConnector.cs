using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Outloud.Rss.Controllers;

namespace Outloud.Rss
{
    internal class DatabaseConnector : DbContext
    {
        private ILogger<RssController>? _logger;

        internal DatabaseConnector()
        {
            SQLitePCL.Batteries.Init();

            Database.OpenConnection();
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=:memory:");

        internal void SetLogger(ILogger<RssController> logger)
        {
            _logger = logger;
        }


        internal async Task AddUrl(Uri uri)
        {
            if (GetAllRssFeed(el => el.Uri == uri).Any())
            {
                _logger?.LogWarning($"Uri already added: {uri}");
                return;
            }

            Models.RssFeed newRss = new()
            {
                Uri = uri,
                IsActive = true
            };

            await RssReader.SetTitle(newRss);

            Add(newRss);

            SaveChanges();
        }

        internal IEnumerable<Models.RssFeed> GetAllRssFeed(Expression<Func<Models.RssFeed, bool>>? expression = default)
        {
            if (expression == default)
                return RssFeeds.ToList();
            else
                return RssFeeds.Where(expression);
        }

        DbSet<Models.RssFeed> RssFeeds { get; set; }
    }
}
