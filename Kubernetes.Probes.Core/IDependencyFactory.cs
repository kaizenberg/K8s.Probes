using System.Collections.Generic;

namespace Kubernetes.Probes.Core
{
    public interface IDependencyFactory
    {
        IList<T> GetAllInstances<T>();
    }
}
