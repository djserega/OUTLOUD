using Quartz;

namespace Outloud.Rss.TimerReader
{
    public class TimerRSSFeedProcessing : IJob
    {
        private static readonly object _lock = new();

        public Task Execute(IJobExecutionContext context)
        {
            Console.WriteLine($"{DateTime.Now} :: Start schedule");

            try
            {
                lock (_lock)
                {
                    JobDataMap jobData = context.JobDetail.JobDataMap;

                    if (jobData.Count > 0)
                    {
                        foreach (KeyValuePair<string, object> item in jobData)
                            ((Action)item.Value).Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }

            Console.WriteLine($"{DateTime.Now} :: End schedule");
            Console.WriteLine($"{DateTime.Now} :: Next fire time UTC > {context.NextFireTimeUtc}");

            return Task.CompletedTask;
        }
    }
}
