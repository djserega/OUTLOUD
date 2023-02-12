using Microsoft.SyndicationFeed.Rss;
using Microsoft.SyndicationFeed;
using System.Xml;
using System.Text;
using Outloud.Rss.Controllers;

namespace Outloud.Rss
{
    internal class RssReader
    {
        private static ILogger<RssController>? _logger;

        internal static void SetLogger(ILogger<RssController> logger)
        {
            _logger = logger;
        }

        internal static async Task SetTitle(Models.RssFeed rssFeed)
        {
            if (rssFeed.Uri == default)
            {
                _logger?.LogError($"Url is empty. Id: {rssFeed.Id}");
                throw new ArgumentException($"Url is empty. Id: {rssFeed.Id}");
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            using var xmlReader = XmlReader.Create(rssFeed.Uri.ToString(), new XmlReaderSettings()
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
                                rssFeed.Title = content.Value;
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

        internal async static Task<int> ReadItems(Models.RssFeed rssFeed, DateTimeOffset dateFrom = default, uint numberOfNewsToDownload = 10)
        {
            int downloadedNews = 0;

            if (rssFeed.Uri == default)
            {
                _logger?.LogError($"Url is empty. Id: {rssFeed.Id}");
                throw new ArgumentException($"Url is empty. Id: {rssFeed.Id}");
            }

            if (dateFrom == default)
                dateFrom = DateTimeOffset.MinValue;

            using var xmlReader = XmlReader.Create(rssFeed.Uri.ToString(), new XmlReaderSettings() { Async = true });

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

                            if (rssFeed.ItemDatas.Any(el => el.Equals(newItemRss)))
                                continue;

                            downloadedNews++;

                            rssFeed.ItemDatas.Add(newItemRss);

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
    }
}
