using Lamar;
using System.Collections.Generic;
using System.Linq;

namespace Kubernetes.Probes.Core
{
    public class DependencyFactory : IDependencyFactory
    {
        private readonly IServiceContext _context;

        public DependencyFactory(IServiceContext context)
        {
            _context = context;
        }

        public IList<T> GetAllInstances<T>()
        {
            return _context.GetAllInstances<T>().ToList();
        }
    }
}
