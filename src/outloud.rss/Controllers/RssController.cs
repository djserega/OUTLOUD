using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.ComponentModel.DataAnnotations;

namespace Outloud.Rss.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class RssController : ControllerBase
    {
        private static readonly DatabaseConnector _databaseConnector;

        private readonly ILogger<RssController> _logger;

        static RssController()
        {
            _databaseConnector = new();
        }

        public RssController(ILogger<RssController> logger)
        {
            _logger = logger;
            _databaseConnector.SetLogger(_logger);
        }

        [HttpPost("AddRSSFeed")]
        public async Task<string> AddRSSFeed([Required] string feedUrl)
        {
            try
            {
                CheckToCorrectUrl(feedUrl, out Uri? uri);

                await _databaseConnector.AddUrl(uri!);

                return $"{{\"success\": true}}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw new BadHttpRequestException(ex.Message);
            }
        }

        [HttpGet("GetAllActiveRSSFeeds")]
        public IEnumerable<Models.ResultItems.RssFeedResult>? GetAllActiveRSSFeeds()
        {
            try
            {
                List<Models.ResultItems.RssFeedResult> resultsData = new();
                foreach (Models.RssFeed item in _databaseConnector.GetAllRssFeed(el => el.IsActive))
                    resultsData.Add(new Models.ResultItems.RssFeedResult(item));

                return resultsData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw new BadHttpRequestException(ex.Message, 500);
            }
        }

        [HttpGet("GetUnreadNews")]
        public IEnumerable<Models.ResultItems.RssFeedResultWithData>? GetUnreadNews(
            [Required] DateTimeOffset dateFrom,
            string? feedUrl = default,
            uint numberOfNewsToDownloadPerUrl = 10,
            bool markedIsRead = false)
        {
            try
            {
                Expression<Func<Models.RssFeed, bool>> expressionFindFeed;
                if (feedUrl == default)
                    expressionFindFeed = el => el.IsActive;
                else
                {
                    CheckToCorrectUrl(feedUrl, out Uri? uri);

                    expressionFindFeed = el => el.IsActive && el.Uri == uri;
                }

                IEnumerable<Models.RssFeed> feeds = _databaseConnector.GetAllRssFeed(expressionFindFeed);

                for (int i = 0; i < feeds.Count(); i++)
                {
                    // fill the rss item with news 
                    RssReader.ReadItems(feeds.ElementAt(i), dateFrom, numberOfNewsToDownloadPerUrl);
                }

                // get unread news, and mark them as IsRead
                List<Models.ResultItems.RssFeedResultWithData> feedsNoRead = new();
                foreach (Models.RssFeed item in feeds.Where(el => el.ItemDatas.Any(el => !el.IsRead && el.DatePublication >= dateFrom)))
                {
                    Models.ResultItems.RssFeedResultWithData unreadRssItem = new(item);

                    foreach (Models.RssFeedItemData itemData in item.ItemDatas)
                    {
                        if (!itemData.IsRead)
                        {
                            unreadRssItem.ItemDatas.Add(new Models.ResultItems.RssFeedResultWithDataItem(itemData));

                            // mark collected news as IsRead
                            if (markedIsRead)
                                itemData.IsRead = true;
                        }
                    }

                    feedsNoRead.Add(unreadRssItem);
                }

                _databaseConnector.SaveChanges();

                return feedsNoRead;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw new BadHttpRequestException(ex.Message, 500);
            }
        }

        [HttpPost("SetNewAsRead")]
        public string? SetNewAsRead(string feedUrl = "")
        {
            try
            {
                Expression<Func<Models.RssFeed, bool>> expressionFindFeed;
                if (feedUrl == default)
                    expressionFindFeed = el => el.IsActive;
                else
                {
                    CheckToCorrectUrl(feedUrl, out Uri? uri);

                    expressionFindFeed = el => el.IsActive && el.Uri == uri;
                }

                int numNewsMarked = 0;

                IEnumerable<Models.RssFeed> feeds = _databaseConnector.GetAllRssFeed(expressionFindFeed);
                foreach (Models.RssFeed item in feeds.Where(el => el.ItemDatas.Any(el => !el.IsRead)))
                {
                    foreach (Models.RssFeedItemData itemData in item.ItemDatas)
                    {
                        if (!itemData.IsRead)
                        {
                            itemData.IsRead = true;
                            numNewsMarked++;
                        }
                    }
                }

                _databaseConnector.SaveChanges();

                return $"Number of downloaded news marked as read: {numNewsMarked}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw new BadHttpRequestException(ex.Message, 500);
            }
        }


        private static void CheckToCorrectUrl(string? feedUrl, out Uri? uri)
        {
            uri = default;

            if (Uri.TryCreate(feedUrl, UriKind.Absolute, out Uri? checkUri))
                uri = checkUri;
            else
                throw new InvalidOperationException("Url is not valid");
        }

    }
}