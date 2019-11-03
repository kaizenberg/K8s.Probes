using Kubernetes.Probes.Core;
using Microsoft.Azure.ServiceBus.Management;
using System.Threading;
using System.Threading.Tasks;

namespace Kubernetes.Probes.Worker
{
    internal class QueueDependency : IServiceDependency
    {
        private readonly string _queueConnectionString;
        private readonly string _queueName;

        public QueueDependency(string queueConnectionString, string queueName)
        {
            _queueConnectionString = queueConnectionString;
            _queueName = queueName;
        }

        public async Task<bool> CheckAsync(CancellationToken token)
        {
            var nsManager = new ManagementClient(_queueConnectionString);

            return await nsManager.QueueExistsAsync(_queueName).ConfigureAwait(false);
        }
    }
}