using Kubernetes.Probes.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kubernetes.Probes.Worker
{
    public class ServiceImplementation : IServiceImplementation
    {
        private readonly ILogger<ServiceImplementation> _logger;

        public ServiceImplementation(ILogger<ServiceImplementation> logger)
        {
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
            }
        }
    }
}
