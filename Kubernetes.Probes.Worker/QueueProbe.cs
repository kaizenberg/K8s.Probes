using Kubernetes.Probes.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Kubernetes.Probes.Worker
{
    public class QueueProbe : IStartupActivity
    {
        private readonly string _queueName;
        private readonly AppConfig _config;
        private readonly ILogger<QueueProbe> _logger;

        public QueueProbe(
            string queueName,
            IOptions<AppConfig> config,
            ILogger<QueueProbe> logger)
        {
            _queueName = queueName;
            _config = config.Value;
            _logger = logger;
        }

        public async Task<bool> ExecuteAsync(CancellationToken ct)
        {
            _logger.LogInformation($"Checking dependency: {_queueName}");

            var result = false;
            var token = await AzureAuthenticator.AcquireTokenByServicePrincipal(_config.TenantId, _config.ClientId, _config.ClientSecret);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.BaseAddress = new Uri("https://management.azure.com/");

                var url = $"/subscriptions/{_config.SubscriptionId}/resourceGroups/{_config.ResourceGroupName}/providers/Microsoft.ServiceBus/namespaces/{_config.ServiceBusNamespace}/queues/{_queueName}?api-version=2017-04-01";

                using (var response = await client.GetAsync(url))
                {
                    result = response.IsSuccessStatusCode;
                }
            }

            _logger.LogInformation(result ? $"{_queueName} is accessible" : $"{_queueName} isn't accessible");

            if (!result) throw new StartupActivityException(this);

            return result;
        }
    }
}
