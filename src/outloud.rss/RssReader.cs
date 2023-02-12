using Microsoft.SyndicationFeed.Rss;
using Microsoft.SyndicationFeed;
using System.Xml;
using System.Text;
using Outloud.Rss.Controllers;
using System.Linq.Expressions;

namespace Outloud.Rss
{
    internal class RssReader
    {
        private readonly ILogger<RssController> _logger;
        private readonly IDatabaseConnector? _dbConnector;
        private readonly Models.RssFeed? _rssFeed;

        internal RssReader(ILogger<RssController> logger)
        {
            _logger = logger;
        }
        internal RssReader(ILogger<RssController> logger, IDatabaseConnector dbConnector) : this(logger)
        {
            _dbConnector = dbConnector;
        }
        internal RssReader(ILogger<RssController> logger, Models.RssFeed rssFeed) : this(logger)
        {
            _rssFeed = rssFeed;
        }
        internal RssReader(ILogger<RssController> logger, IDatabaseConnector dbConnector, Models.RssFeed rssFeed) : this(logger, rssFeed)
        {
            _dbConnector = dbConnector;
        }

        internal async Task SetTitle()
        {
            if (_rssFeed.Uri == default)
            {
                _logger?.LogError($"Url is empty. Id: {_rssFeed.Id}");
                throw new ArgumentException($"Url is empty. Id: {_rssFeed.Id}");
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            using var xmlReader = XmlReader.Create(_rssFeed.Uri.ToString(), new XmlReaderSettings()
            {
                Async = true
            });

            var feedReader = new RssFeedReader(xmlReader);

            try
            {
                while (await feedReader.Read())
                {
                    switch (feedReader.ElementType)
                    {
                        default:
                            ISyndicationContent content = await feedReader.ReadContent();
                            if (content.Name.Equals("title"))
                            {
                                _rssFeed.Title = content.Value;
                                return;
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.ToString());
                throw new InvalidOperationException(ex.Message);
            }
        }

        internal async Task<int> ReadItems(DateTimeOffset dateFrom = default,
                                           uint numberOfNewsToDownload = 10)
        {
            int downloadedNews = 0;

            if (_rssFeed.Uri == default)
            {
                _logger?.LogError($"Url is empty. Id: {_rssFeed.Id}");
                throw new ArgumentException($"Url is empty. Id: {_rssFeed.Id}");
            }

            if (dateFrom == default)
                dateFrom = DateTimeOffset.MinValue;

            using var xmlReader = XmlReader.Create(_rssFeed.Uri.ToString(), new XmlReaderSettings() { Async = true });

            var feedReader = new RssFeedReader(xmlReader);

            uint downloadNews = 0;

            ISyndicationItem item;

            while (await feedReader.Read())
            {
                try
                {
                    switch (feedReader.ElementType)
                    {
                        case SyndicationElementType.Item:

                            item = await feedReader.ReadItem();

                            if (item.Published < dateFrom)
                                return downloadedNews;

                            Models.RssFeedItemData newItemRss = new()
                            {
                                DatePublication = item.Published,
                                Description = item.Description,
                                Links = string.Join("; ", item.Links.Select(el => el.Uri.ToString())),
                                Title = item.Title
                            };

                            if (_rssFeed.ItemDatas.Any(el => el.Equals(newItemRss)))
                                continue;

                            downloadedNews++;

                            _rssFeed.ItemDatas.Add(newItemRss);

                            if (++downloadNews >= numberOfNewsToDownload)
                                return downloadedNews;

                            break;
                    }
                }
                catch (FormatException ex)
                {
                    _logger?.LogWarning(ex.ToString());
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex.ToString());
                }
            }

            return downloadedNews;
        }

        internal async Task<IEnumerable<Models.RssFeed>> DownloadingNewFromActiveRss(DateTimeOffset dateFrom = default,
                                                                                    string? feedUrl = default,
                                                                                    uint numberOfNewsToDownloadPerUrl = 10)
        {
            if (_dbConnector == default)
            {
                _logger?.LogError($"Database connector is unassigned");
                throw new ArgumentException($"Database connector is unassigned");
            }

            _logger.LogInformation("News loading has started");

            Expression<Func<Models.RssFeed, bool>> expressionFindFeed;
            if (feedUrl == default)
                expressionFindFeed = el => el.IsActive;
            else
            {
                RssReader.CheckToCorrectUrl(feedUrl, out Uri? uri);

                expressionFindFeed = el => el.IsActive && el.Uri == uri;
            }

            IEnumerable<Models.RssFeed>? feeds = await _dbConnector.GetAllRssFeed(expressionFindFeed);

            _logger.LogInformation($"Number of active rss: {feeds.Count()}");

            int downloadedNews = 0;
            for (int i = 0; i < feeds.Count(); i++)
            {
                RssReader rssReader = new(_logger, _dbConnector, feeds.ElementAt(i));
                downloadedNews += await rssReader.ReadItems(dateFrom, numberOfNewsToDownloadPerUrl);
            }

            _logger.LogInformation($"The news has been downloaded: {downloadedNews}");

            if (downloadedNews > 0)
            {
                _logger.LogInformation("Saving data");

                _dbConnector.SaveChanges();

                _logger.LogInformation("Data saved");
            }

            return feeds;
        }

        internal static void CheckToCorrectUrl(string? feedUrl,
                                               out Uri? uri)
        {
            uri = default;

            if (Uri.TryCreate(feedUrl, UriKind.Absolute, out Uri? checkUri))
                uri = checkUri;
            else
                throw new InvalidOperationException("Url is not valid");
        }
    }
}
