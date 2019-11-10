using Microsoft.Extensions.Logging;
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
    public class ServiceWrapper : Microsoft.Extensions.Hosting.BackgroundService
    {
        private readonly IList<IServiceDependency> _dependencyChecks;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly ILogger<ServiceWrapper> _logger;
        private readonly IServiceImplementation _service;

        public ServiceWrapper(
            IDependencyFactory dependencyFacotry,
            ILogger<ServiceWrapper> logger,
            IServiceImplementation service)
        {
            _dependencyChecks = dependencyFacotry.GetAllInstances<IServiceDependency>();
            _logger = logger;
            _service = service;

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
            await SendReadinessAsync(token).ConfigureAwait(false);

            var signalParallelTask = SendLivelinessAsync(TimeSpan.FromSeconds(15), token);
            var workerParallelTask = _service.ExecuteAsync(token);

            await Task.WhenAny(signalParallelTask, workerParallelTask)
                .ContinueWith(t => CancellationTokenSource.CreateLinkedTokenSource(token).Cancel()).ConfigureAwait(false);
        }

        private async Task SendReadinessAsync(CancellationToken token)
        {
            await _retryPolicy.ExecuteAsync(async (ctx, ct) =>
            {
                var resultTasks = await Task.WhenAll(_dependencyChecks.Select(t => t.CheckAsync(ct))).ConfigureAwait(false);

                if (resultTasks.All(result => result))
                    await File.WriteAllTextAsync("ready.txt", DateTime.UtcNow.ToString(), token).ConfigureAwait(false);
            },
            new Context("ready"), token).ConfigureAwait(false);
        }

        private async Task SendLivelinessAsync(TimeSpan interval, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await _retryPolicy.ExecuteAsync((ctx, ct) => File.WriteAllTextAsync("alive.txt", DateTime.UtcNow.ToString(), ct),
                    new Context("alive"), token).ConfigureAwait(false);

                await Task.Delay(interval, token).ConfigureAwait(false);
            }
        }
    }
}
