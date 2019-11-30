namespace Kubernetes.Probes.Core
{
    public class ProbeConfig
    {
        public int LivenessSignalIntervalSeconds { get; set; }

        public string LivenessFilePath { get; set; }

        public string StartupFilePath { get; set; }
    }
}