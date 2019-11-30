using System;

namespace Kubernetes.Probes.Core
{
    public class StartupActivityException : Exception
    {
        public StartupActivityException(object activity) : base($"{ activity.GetType().Name} startup activity failed.")
        {
        }
    }
}
