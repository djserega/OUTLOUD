using Outloud.Rss.Controllers;
using Quartz;
using Quartz.Impl;

namespace Outloud.Rss.TimerReader
{
    internal class TimerRSSFeed
    {
        private readonly ILogger<RssController> _logger;
        private readonly IDatabaseConnector _databaseConnector;

        private Action? _actionReader;

        private IScheduler? _schedulerReaderRss;

        public TimerRSSFeed(ILogger<RssController> logger,
                            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _databaseConnector = serviceProvider.GetRequiredService<DatabaseConnector>();

            Task.Run(InitAutodownloadNews);
        }

        private async Task InitAutodownloadNews()
        {
            _logger.LogInformation("Initializing autoreader news");

            await SetAction(new Action(async () =>
            {
                RssReader rssReader = new(_logger, _databaseConnector);
                await rssReader.DownloadingNewFromActiveRss(numberOfNewsToDownloadPerUrl: 50);
            }));

            _logger.LogInformation("Autoreader news initialized");
        }

        internal async Task SetAction(Action actionReader)
        {
            if (_actionReader == default)
            {
                _schedulerReaderRss = await new StdSchedulerFactory().GetScheduler();

                _actionReader = actionReader;

                try
                {
                    await StartReaderAsync(0, 0, 5);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex.Message);
                }

                await _schedulerReaderRss.Start();
            }
        }

        internal async Task<DateTimeOffset?> StartReaderAsync(int hours,
                                                              int minutes,
                                                              int seconds)
        {
            if (_schedulerReaderRss == default)
            {
                _logger?.LogError(new ArgumentNullException("Scheduler is unassigned"), "Scheduler is unassigned");
                return default;
            }
            if (_actionReader == default)
            {
                _logger?.LogError(new ArgumentNullException("Action is unassigned"), "Action is unassigned");
                return default;
            }

            string jobName = "DataReader";

            _ = await _schedulerReaderRss.DeleteJob(new JobKey(jobName));

            JobDataMap jobDataMap = new(
                new Dictionary<string, Action>()
                {
                    { jobName, () => _actionReader() }
                });


            IJobDetail job = JobBuilder.Create<TimerRSSFeedProcessing>()
                .WithIdentity(jobName)
                .UsingJobData(jobDataMap)
                .Build();

            ITrigger trigger = TriggerBuilder.Create()
                .WithSimpleSchedule(x => x
                    .WithInterval(new TimeSpan(hours, minutes, seconds))
                    .RepeatForever())
                .ForJob(jobName)
                .Build();

            return await _schedulerReaderRss.ScheduleJob(job, trigger);
        }
    }
}
