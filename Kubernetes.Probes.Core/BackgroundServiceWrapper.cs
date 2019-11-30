using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Kubernetes.Probes.Core
{
    public class BackgroundServiceWrapper : BackgroundService
    {
        private readonly IEnumerable<IStartupActivity> _startupActivities;
        private readonly IServiceImplementation _serviceImplementation;
        private readonly ProbeConfig _probeConfiguration;

        public AsyncRetryPolicy SignalRetryPolicy { get; protected set; }

        public ILogger<BackgroundServiceWrapper> LoggerInstance { get; protected set; }

        public BackgroundServiceWrapper(
            IEnumerable<IStartupActivity> startupActivities,
            ILogger<BackgroundServiceWrapper> loggerInstance,
            IServiceImplementation serviceImplementation,
            IOptions<ProbeConfig> probeConfiguration)
        {
            _startupActivities = startupActivities;
            LoggerInstance = loggerInstance;
            _serviceImplementation = serviceImplementation;
            _probeConfiguration = probeConfiguration.Value;

            var currentDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            _probeConfiguration.StartupFilePath = _probeConfiguration.StartupFilePath ?? Path.Combine(currentDirectory, "started.signal");
            _probeConfiguration.LivenessFilePath = _probeConfiguration.LivenessFilePath ?? Path.Combine(currentDirectory, "alive.signal");

            // Retry policy for all started and alive signals. Retries happen with exponential delay for only 3 times.
            SignalRetryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        LoggerInstance.LogInformation($"Uable to send {context.OperationKey} signal. Retrying...");
                    });
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            // Create a token that can be cancelled to stop all running tasks
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

            // Execute startup activities and upon success send a startup signal
            await SendStartedSignalAsync(tokenSource.Token).ConfigureAwait(false);

            // Start sending liveness signal periodically
            var signalParallelTask = SendAliveSignalAsync(TimeSpan.FromSeconds(_probeConfiguration.LivenessSignalIntervalSeconds), tokenSource.Token);

            // Paralllelly execute background worker's jobs
            var workerParallelTask = _serviceImplementation.ExecuteAsync(tokenSource.Token);

            // If liveness or worker job throws exception or runs to completion then cancel everything and exit the program.
            var continuationTask = await Task.WhenAny(signalParallelTask, workerParallelTask).ConfigureAwait(false);

            // Awaiting on task returned by Task.WhenAny() bubbles exceptions in inner tasks
            await continuationTask.ConfigureAwait(false);

            // If any one of then run to completion then cancel the other
            await continuationTask.ContinueWith(t => tokenSource.Cancel()).ConfigureAwait(false);

            // Although all tasks will finish by now, background service will not cause the host to terminate.
            // You will have to implement IApplicationLifetime for that which may not make sense if there are multiple IHostedService implemented.
            // But, since kubernetes liveness probes are enabled, absence of alive.signal file will cause service to reboot.
        }

        protected virtual async Task SendStartedSignalAsync(CancellationToken token)
        {
            await SignalRetryPolicy.ExecuteAsync(async (ctx, ct) =>
            {
                var resultTasks = await Task.WhenAll(_startupActivities.Select(t => t.ExecuteAsync(ct))).ConfigureAwait(false);

                if (resultTasks.All(result => result))
                    await File.WriteAllTextAsync(_probeConfiguration.StartupFilePath, DateTime.UtcNow.ToString(), token).ConfigureAwait(false);
            },
            new Context("started"), token).ConfigureAwait(false);

            LoggerInstance.LogInformation("Sent startup signal");
        }

        protected virtual async Task SendAliveSignalAsync(TimeSpan interval, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await SignalRetryPolicy.ExecuteAsync((ctx, ct) => File.WriteAllTextAsync(_probeConfiguration.LivenessFilePath, DateTime.UtcNow.ToString(), ct),
                    new Context("alive"), token).ConfigureAwait(false);

                LoggerInstance.LogInformation("Sent liveness signal");

                await Task.Delay(interval, token).ConfigureAwait(false);
            }
        }
    }
}