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
        }

        internal void Init()
        {
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
            IEnumerable<Models.RssFeed> rssFeeds = await GetAllRssFeed(el => el.Uri == uri);
            if (rssFeeds.Any())
            {
                _logger?.LogWarning($"Uri already added: {uri}");
                return;
            }

            Models.RssFeed newRss = new()
            {
                Uri = uri,
                IsActive = true
            };

            if (_logger != default)
                RssReader.SetLogger(_logger);

            await RssReader.SetTitle(newRss);

            Add(newRss);

            SaveChanges();
        }

        internal async Task<IEnumerable<Models.RssFeed>> GetAllRssFeed(Expression<Func<Models.RssFeed, bool>>? expression = default)
        {
            if (expression == default)
                return await RssFeeds.ToListAsync();
            else
                return await RssFeeds.Where(expression).ToListAsync();
        }

        DbSet<Models.RssFeed> RssFeeds { get; set; }
    }
}
