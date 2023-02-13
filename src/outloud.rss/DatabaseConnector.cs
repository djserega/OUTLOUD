using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Outloud.Rss.Controllers;

namespace Outloud.Rss
{
    internal class DatabaseConnector : DbContext, IDatabaseConnector
    {
        private readonly ILogger<RssController>? _logger;

        public DatabaseConnector(ILogger<RssController> logger)
        {
            _logger = logger;

            SQLitePCL.Batteries.Init();

            Database.OpenConnection();
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=:memory:");

        public async Task AddUrl(Uri uri)
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

            RssReader rssReader = new(_logger, newRss);
            await rssReader.SetTitle();

            Add(newRss);

            SaveChanges();
        }

        public async Task<IEnumerable<Models.RssFeed>> GetAllRssFeed(Expression<Func<Models.RssFeed, bool>>? expression = default)
        {
            if (expression == default)
                return await RssFeeds.ToListAsync();
            else
                return await RssFeeds.Where(expression).ToListAsync();
        }

        DbSet<Models.RssFeed> RssFeeds { get; set; }
    }
}
