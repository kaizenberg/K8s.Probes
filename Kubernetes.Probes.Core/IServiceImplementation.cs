using System.Threading;
using System.Threading.Tasks;

namespace Kubernetes.Probes.Core
{
    public interface IServiceImplementation
    {
        Task ExecuteAsync(CancellationToken token);
    }
}
