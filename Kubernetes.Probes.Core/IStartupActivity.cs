using System.Threading;
using System.Threading.Tasks;

namespace Kubernetes.Probes.Core
{
    public interface IStartupActivity
    {
        Task<bool> ExecuteAsync(CancellationToken token);
    }
}
