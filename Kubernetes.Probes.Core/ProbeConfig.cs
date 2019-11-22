namespace Kubernetes.Probes.Core
{
    public class ProbeConfig
    {
        public int AliveFileCreationIntervalSeconds { get; set; }

        public string AliveFilePath { get; set; }

        public string ReadyFilePath { get; set; }
    }
}