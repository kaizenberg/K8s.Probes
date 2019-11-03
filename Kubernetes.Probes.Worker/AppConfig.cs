using Microsoft.Extensions.Logging;

namespace Kubernetes.Probes.Worker
{
    public class AppConfig
    {
        public LogLevel LogLevel { get; set; }

        public string ResponseQueueConnection { get; set; }

        public string RequestQueueConnection { get; set; }

        public string RequestQueueName { get; set; }

        public string ResponseQueueName { get; set; }
    }
}