using Outloud.Rss.Controllers;
using Quartz;
using Quartz.Impl;
using System.ComponentModel.DataAnnotations;

namespace Outloud.Rss.TimerReader
{
    internal class TimerRSSFeed
    {
        private ILogger<RssController>? _logger;
        private Action? _actionReader;

        private IScheduler? _schedulerReaderRss;

        public TimerRSSFeed()
        {
            Task.Run(async () =>
            {
                _schedulerReaderRss = await new StdSchedulerFactory().GetScheduler();

                await _schedulerReaderRss.Start();
            });
        }

        internal void SetLogger(ILogger<RssController> logger)
        {
            if (logger == default)
                _logger = logger;
        }
        internal async void SetAction(Action actionReader)
        {
            if (_actionReader == default)
            {
                _actionReader = actionReader;

                //try
                //{
                //    await StartReaderAsync(0, 0, 10);
                //}
                //catch (Exception ex)
                //{
                //    _logger?.LogError(ex.Message);
                //}
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
