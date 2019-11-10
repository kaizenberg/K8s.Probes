using Microsoft.Extensions.Logging;

namespace Kubernetes.Probes.Worker
{
    public class AppConfig
    {
        public LogLevel LogLevel { get; set; }

        public string ResponseQueueConnectionString { get; set; }

        public string RequestQueueConnectionString { get; set; }

        public string RequestQueue { get; set; }

        public string ResponseQueue { get; set; }

        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        public string ResourceGroup { get; set; }

        public string SubscriptionId { get; set; }

        public string TenantId { get; set; }

        public object ServiceBusNamespace { get; set; }
    }
}