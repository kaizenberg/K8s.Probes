namespace Kubernetes.Probes.Worker
{
    public class AppConfig
    {
        // Azure Service Principal credentials
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }

        // Azure Account
        public string SubscriptionId { get; set; }
        public string TenantId { get; set; }

        // Azure Resource Group on which above Service Principal has read-only/reader access. All of the services inside this group can be probed. 
        public string ResourceGroupName { get; set; }

        // Sample Service Bus that is probed
        public object ServiceBusNamespace { get; set; }
        public string ServiceBusNamespaceSASKey { get; set; } // Used for worker's job. Required Read/Write access on queues. Especially response queue.
        public string RequestQueueName { get; set; }
        public string ResponseQueueName { get; set; }
    }
}