using Outloud.Rss.Controllers;
using Quartz;
using Quartz.Impl;

namespace Outloud.Rss.TimerReader
{
    internal class TimerRSSFeed
    {
        private ILogger<RssController>? _logger;
        private Action? _actionReader;

        private IScheduler? _schedulerReaderRss;

        internal bool Initialized { get => _schedulerReaderRss != default; }

        internal void SetLogger(ILogger<RssController> logger)
        {
            if (_logger == default)
                _logger = logger;
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

        internal async Task<DateTimeOffset?> StartReaderAsync(int hours, int minutes, int seconds)
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
