using Kubernetes.Probes.Core;
using Lamar;
using Lamar.Microsoft.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Kubernetes.Probes.Worker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var builder = new HostBuilder()
                    .UseLamar()
                    .ConfigureServices((context, services) =>
                    {
                        var registry = new ServiceRegistry();

                        registry.Scan(s =>
                            {
                                s.TheCallingAssembly();
                                s.WithDefaultConventions();
                            });

                        registry.Configure<AppConfig>(option =>
                        {
                            option.RequestQueueConnection = Environment.GetEnvironmentVariable("RequestQueueConnection");
                            option.ResponseQueueConnection = Environment.GetEnvironmentVariable("ResponseQueueConnection");
                            option.RequestQueueName = Environment.GetEnvironmentVariable("RequestQueueName");
                            option.ResponseQueueName = Environment.GetEnvironmentVariable("ResponseQueueName");

                            short.TryParse(Environment.GetEnvironmentVariable("LogLevel"),
                                out short logLevel);

                            option.LogLevel = (Microsoft.Extensions.Logging.LogLevel)logLevel;

                            var config = new NLog.Config.LoggingConfiguration();

                            // Targets where to log to: File & Console
                            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "service.log" };
                            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

                            // Rules for mapping loggers to targets            
                            config.AddRule(NLog.LogLevel.FromOrdinal(logLevel), NLog.LogLevel.Fatal, logconsole);
                            config.AddRule(NLog.LogLevel.FromOrdinal(logLevel), NLog.LogLevel.Fatal, logfile);

                            // Apply config           
                            NLog.LogManager.Configuration = config;
                        });

                        var qConfig = new Container(registry).GetInstance<IOptions<AppConfig>>().Value;

                        registry.For<IServiceDependency>()
                        .Use<QueueDependency>()
                        .Ctor<string>("queueConnectionString").Is(qConfig.RequestQueueConnection)
                        .Ctor<string>("queueName").Is(qConfig.RequestQueueName)
                        .Named(qConfig.RequestQueueName);

                        registry.For<IServiceDependency>()
                        .Use<QueueDependency>()
                        .Ctor<string>("queueConnectionString").Is(qConfig.ResponseQueueConnection)
                        .Ctor<string>("queueName").Is(qConfig.ResponseQueueName)
                        .Named(qConfig.ResponseQueueName);

                        registry.For<IDependencyFactory>().Use<DependencyFactory>();

                        registry.For<IHostedService>().Use<ServiceWrapper>();

                        services.AddLamar(registry);
                    })
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                    })
                    .UseNLog()
                    .UseConsoleLifetime();

                // Build and run the host in one go; .RCA is specialized for running it in a console.
                // It registers SIGTERM(Ctrl-C) to the CancellationTokenSource that's shared with all services in the container.
                await builder.RunConsoleAsync().ConfigureAwait(false);

                NLog.LogManager.GetCurrentClassLogger().Info("The host container has terminated. Press ANY key to exit the console.");
            }
            catch (Exception ex)
            {
                // NLog: catch setup errors (exceptions thrown inside of any containers may not necessarily be caught)
                NLog.LogManager.GetCurrentClassLogger().Fatal(ex, "Stopped program because of exception");
                throw;
            }
            finally
            {
                // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
                LogManager.Shutdown();
            }
        }
    }
}
