using System.Threading;
using System.Threading.Tasks;

namespace Kubernetes.Probes.Core
{
    public interface IServiceDependency
    {
        Task<bool> CheckAsync(CancellationToken token);
    }
}
