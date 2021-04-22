namespace MassTransit.JobService.Components.Consumers
{
    using System;
    using System.Collections;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.JobService;
    using Metadata;


    public class FaultJobConsumer<TJob> :
        IConsumer<FaultJob>
        where TJob : class
    {
        readonly string _jobConsumerTypeName;
        readonly IJobService _jobService;
        readonly JobOptions<TJob> _options;

        public FaultJobConsumer(IJobService jobService, JobOptions<TJob> options, string jobConsumerTypeName)
        {
            _jobService = jobService;
            _options = options;
            _jobConsumerTypeName = jobConsumerTypeName;
        }

        public Task Consume(ConsumeContext<FaultJob> context)
        {
            var faultJob = context.Message;

            var job = faultJob.GetJob<TJob>();
            if (job == null)
                throw new ArgumentNullException(nameof(faultJob.Job), $"The job could not be deserialized: {TypeMetadataCache<TJob>.ShortName}");

            var jobContext = new ConsumeJobContext<TJob>(context, _jobService.InstanceAddress, faultJob.JobId, faultJob.AttemptId, faultJob.RetryAttempt, job,
                _options.JobTimeout);

            return jobContext.NotifyFaulted(faultJob.Duration ?? TimeSpan.Zero, _jobConsumerTypeName, new JobFaultedException(faultJob.Exceptions));
        }


        class JobFaultedException :
            MassTransitException
        {
            readonly IDictionary _data;
            readonly ExceptionInfo _exceptionInfo;

            public JobFaultedException(ExceptionInfo exceptionInfo)
                : base(exceptionInfo.Message, exceptionInfo.InnerException != null ? new JobFaultedException(exceptionInfo.InnerException) : default)
            {
                _exceptionInfo = exceptionInfo;
                if (_exceptionInfo.Data != null)
                    _data = _exceptionInfo.Data.ToDictionary(x => x.Key);
            }

            public override string StackTrace => _exceptionInfo.StackTrace;
            public override string Source => _exceptionInfo.Source;
            public override IDictionary Data => _data;
        }
    }
}
