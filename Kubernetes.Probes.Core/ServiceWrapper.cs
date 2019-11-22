using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kubernetes.Probes.Core
{
    public class ServiceWrapper : BackgroundService
    {
        private readonly IList<IServiceDependency> _dependencyChecks;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly ILogger<ServiceWrapper> _logger;
        private readonly IServiceImplementation _service;
        private readonly ProbeConfig _config;

        public ServiceWrapper(
            IDependencyFactory dependencyFacotry,
            ILogger<ServiceWrapper> logger,
            IServiceImplementation service,
            IOptions<ProbeConfig> config)
        {
            _dependencyChecks = dependencyFacotry.GetAllInstances<IServiceDependency>();
            _logger = logger;
            _service = service;
            _config = config.Value;

            _config.ReadyFilePath = _config.ReadyFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), "ready.txt");
            _config.AliveFilePath = _config.AliveFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), "alive.txt");

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogInformation($"Couldn't send {context.OperationKey} signal: {exception.ToString()}");
                    });
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

            await SendReadinessAsync(tokenSource.Token).ConfigureAwait(false);

            var signalParallelTask = SendLivelinessAsync(TimeSpan.FromSeconds(_config.AliveFileCreationIntervalSeconds), tokenSource.Token);
            var workerParallelTask = _service.ExecuteAsync(tokenSource.Token);

            var continuationTask = await Task.WhenAny(signalParallelTask, workerParallelTask);

            try
            {
                await continuationTask;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Something went wrong. {ex.ToString()}");
            }
            finally
            {
                await continuationTask.ContinueWith((t) => tokenSource.Cancel());
            }
        }

        private async Task SendReadinessAsync(CancellationToken token)
        {
            await _retryPolicy.ExecuteAsync(async (ctx, ct) =>
            {
                var resultTasks = await Task.WhenAll(_dependencyChecks.Select(t => t.TestAsync(ct))).ConfigureAwait(false);

                if (resultTasks.All(result => result))
                    await File.WriteAllTextAsync(_config.ReadyFilePath, DateTime.UtcNow.ToString(), token).ConfigureAwait(false);
            },
            new Context("ready"), token).ConfigureAwait(false);

            _logger.LogInformation("Created ready.txt file");
        }

        private async Task SendLivelinessAsync(TimeSpan interval, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await _retryPolicy.ExecuteAsync((ctx, ct) => File.WriteAllTextAsync(_config.AliveFilePath, DateTime.UtcNow.ToString(), ct),
                    new Context("alive"), token).ConfigureAwait(false);

                _logger.LogInformation("Created alive.txt file");

                await Task.Delay(interval, token).ConfigureAwait(false);
            }
        }
    }
}
