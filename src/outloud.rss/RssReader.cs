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

        internal async static void ReadItems(Models.RssFeed rssFeed, DateTimeOffset dateFrom = default, uint numberOfNewsToDownload = 10)
        {
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

            try
            {
                while (await feedReader.Read())
                {
                    switch (feedReader.ElementType)
                    {
                        case SyndicationElementType.Item:

                            ISyndicationItem item = await feedReader.ReadItem();

                            if (item.Published < dateFrom)
                                return;

                            Models.RssFeedItemData newItemRss = new()
                            {
                                DatePublication = item.Published,
                                Description = item.Description,
                                Links = string.Join("; ", item.Links.Select(el => el.Uri.ToString())),
                                Title = item.Title
                            };

                            if (rssFeed.ItemDatas.Any(el => el.Equals(newItemRss)))
                                continue;

                            rssFeed.ItemDatas.Add(newItemRss);

                            if (++downloadNews >= numberOfNewsToDownload)
                                return;

                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.ToString());
            }
        }
    }
}
