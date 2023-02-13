using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace Outloud.Rss.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class RssController : ControllerBase
    {
        private readonly ILogger<RssController> _logger;
        private readonly IDatabaseConnector _databaseConnector;
        private readonly TimerReader.TimerRSSFeed _timerRSSFeedReader;

        public RssController(ILogger<RssController> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
           
            _databaseConnector = serviceProvider.GetRequiredService<DatabaseConnector>();
            _timerRSSFeedReader = serviceProvider.GetRequiredService<TimerReader.TimerRSSFeed>();
        }

        [HttpPost("AddRSSFeed")]
        public async Task<string> AddRSSFeed([Required] string feedUrl = "https://inform-ua.info/feed/rss/v1")
        {
            try
            {
                _logger.LogInformation($"Adding feed: {feedUrl}");

                RssReader.CheckToCorrectUrl(feedUrl, out Uri? uri);

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

                RssReader rssReader = new(_logger, _databaseConnector);

                IEnumerable<Models.RssFeed> feeds = await rssReader.DownloadingNewFromActiveRss(dateFrom, feedUrl, 0);

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

                Expression<Func<Models.RssFeed, bool>> expressionFindFeed;
                if (string.IsNullOrWhiteSpace(feedUrl))
                    expressionFindFeed = el => el.IsActive;
                else
                {
                    RssReader.CheckToCorrectUrl(feedUrl, out Uri? uri);

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

    }
}