﻿using Outloud.Rss.Controllers;
using System.Linq.Expressions;

namespace Outloud.Rss
{
    public interface IDatabaseConnector
    {
        void SetLogger(ILogger<RssController> logger);
        Task AddUrl(Uri uri);
        Task<IEnumerable<Models.RssFeed>> GetAllRssFeed(Expression<Func<Models.RssFeed, bool>>? expression = default);

        int SaveChanges();
    }
}
