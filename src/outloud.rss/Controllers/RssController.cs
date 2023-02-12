using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.ComponentModel.DataAnnotations;
using Quartz;

namespace Outloud.Rss.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class RssController : ControllerBase
    {
        private static readonly DatabaseConnector _databaseConnector;
        private static readonly TimerReader.TimerRSSFeed _timerRSSFeedReader;

        private readonly ILogger<RssController> _logger;

        static RssController()
        {
            _databaseConnector = new DatabaseConnector();
            _databaseConnector.Init();

            _timerRSSFeedReader = new();
        }

        public RssController(ILogger<RssController> logger)
        {
            _logger = logger;

            _databaseConnector.SetLogger(_logger);
            _timerRSSFeedReader.SetLogger(_logger);
        }

        [HttpPost("AddRSSFeed")]
        public async Task<string> AddRSSFeed([Required] string feedUrl = "https://www.pravda.com.ua/rss/")
        {
            try
            {
                _logger.LogInformation($"Adding feed: {feedUrl}");

                await InitAutodownloadNew();

                CheckToCorrectUrl(feedUrl, out Uri? uri);

                await _databaseConnector.AddUrl(uri!);

                _logger.LogInformation($"Feed added: {feedUrl}");

                return $"{{\"success\": true}}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw new BadHttpRequestException(ex.Message);
            }
        }

        [HttpGet("GetAllActiveRSSFeeds")]
        public async Task<IEnumerable<Models.ResultItems.RssFeedResult>?> GetAllActiveRSSFeeds()
        {
            try
            {
                _logger.LogInformation("Received request active rss");

                await InitAutodownloadNew();

                List<Models.ResultItems.RssFeedResult> resultsData = new();

                IEnumerable<Models.RssFeed> activeRssFeed = await _databaseConnector.GetAllRssFeed(el => el.IsActive);

                foreach (Models.RssFeed item in activeRssFeed)
                    resultsData.Add(new Models.ResultItems.RssFeedResult(item));

                _logger.LogInformation($"Number of active rss {resultsData.Count}");

                return resultsData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw new BadHttpRequestException(ex.Message, 500);
            }
        }

        [HttpGet("GetUnreadNews")]
        public async Task<IEnumerable<Models.ResultItems.RssFeedResultWithData>> GetUnreadNews([Required] DateTimeOffset dateFrom,
                                                                                                string? feedUrl = default,
                                                                                                bool markedIsRead = false)
        {
            try
            {
                string logMessage = $"Received request unread news:\n" +
                    $" - dateFrom: {dateFrom}\n" +
                    $" - feedUrl: {feedUrl}\n" +
                    $" - markedIsRead: {markedIsRead}";
                _logger.LogInformation(logMessage);

                await InitAutodownloadNew();

                IEnumerable<Models.RssFeed> feeds = await DownloadingNewFromActiveRss(_logger, dateFrom, feedUrl, 0);

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

                _logger.LogInformation("Response has been generated");

                return feedsNoRead;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw new BadHttpRequestException(ex.Message, 500);
            }
        }

        [HttpPost("SetNewAsRead")]
        public async Task<string?> SetNewAsRead(string feedUrl = "")
        {
            try
            {
                _logger.LogInformation("Received request marked news as read");

                await InitAutodownloadNew();

                Expression<Func<Models.RssFeed, bool>> expressionFindFeed;
                if (feedUrl == default)
                    expressionFindFeed = el => el.IsActive;
                else
                {
                    CheckToCorrectUrl(feedUrl, out Uri? uri);

                    expressionFindFeed = el => el.IsActive && el.Uri == uri;
                }

                int numNewsMarked = 0;

                IEnumerable<Models.RssFeed> feeds = await _databaseConnector.GetAllRssFeed(expressionFindFeed);
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

        [HttpPost("ChangeTimeLoadingNews")]
        public async Task<Models.ResultItems.TimePeriodDownloadingNews?> ChangeTimeLoadingNews([Required] int hours,
                                                                                               [Required] int minutes,
                                                                                               [Required] int seconds)
        {
            _logger.LogInformation("Trying to change for news download times");

            await InitAutodownloadNew();

            DateTimeOffset? nextStartJob = await _timerRSSFeedReader.StartReaderAsync(hours, minutes, seconds);

            if (nextStartJob == default)
            {
                _logger.LogError("Download task time not set");
                return default;
            }
            else
            {
                return new(hours, minutes, seconds);
            }
        }


        private static void CheckToCorrectUrl(string? feedUrl,
                                              out Uri? uri)
        {
            uri = default;

            if (Uri.TryCreate(feedUrl, UriKind.Absolute, out Uri? checkUri))
                uri = checkUri;
            else
                throw new InvalidOperationException("Url is not valid");
        }

        private async Task InitAutodownloadNew()
        {
            if (!_timerRSSFeedReader.Initialized)
            {
                _logger.LogInformation("Initializing autoreader news");
                
                await _timerRSSFeedReader.SetAction(new Action(async () => 
                {
                    await DownloadingNewFromActiveRss(_logger, numberOfNewsToDownloadPerUrl: 50); 
                }));
                
                _logger.LogInformation("Autoreader news initialized");
            }
        }

        private static async Task<IEnumerable<Models.RssFeed>> DownloadingNewFromActiveRss(ILogger<RssController> logger,
                                                                                           DateTimeOffset dateFrom = default,
                                                                                           string? feedUrl = default,
                                                                                           uint numberOfNewsToDownloadPerUrl = 10)
        {
            logger.LogInformation("News loading has started");

            Expression<Func<Models.RssFeed, bool>> expressionFindFeed;
            if (feedUrl == default)
                expressionFindFeed = el => el.IsActive;
            else
            {
                CheckToCorrectUrl(feedUrl, out Uri? uri);

                expressionFindFeed = el => el.IsActive && el.Uri == uri;
            }

            IEnumerable<Models.RssFeed>? feeds = await _databaseConnector.GetAllRssFeed(expressionFindFeed);

            logger.LogInformation($"Number of active rss: {feeds.Count()}");

            int downloadedNews = 0;
            for (int i = 0; i < feeds.Count(); i++)
            {
                // fill the rss item with news 
                downloadedNews += await RssReader.ReadItems(feeds.ElementAt(i), dateFrom, numberOfNewsToDownloadPerUrl);
            }

            logger.LogInformation($"The news has been downloaded: {downloadedNews}");

            if (downloadedNews > 0)
            {
                logger.LogInformation("Saving data");

                _databaseConnector.SaveChanges();

                logger.LogInformation("Data saved");
            }

            return feeds;
        }

    }
}