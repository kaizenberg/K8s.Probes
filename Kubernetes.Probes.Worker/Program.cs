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

                        registry.Configure<ProbeConfig>(option =>
                        {
                            int.TryParse(Environment.GetEnvironmentVariable("LivenessSignalIntervalSeconds"),
                                out int interval);

                            option.LivenessSignalIntervalSeconds = interval;
                            option.StartupFilePath = Environment.GetEnvironmentVariable("StartupFilePath");
                            option.LivenessFilePath = Environment.GetEnvironmentVariable("LivenessFilePath");
                        });

                        registry.Configure<AppConfig>(option =>
                        {
                            option.SubscriptionId = Environment.GetEnvironmentVariable("SubscriptionId");
                            option.TenantId = Environment.GetEnvironmentVariable("TenantId");
                            option.ClientId = Environment.GetEnvironmentVariable("ClientId");
                            option.ClientSecret = Environment.GetEnvironmentVariable("ClientSecret");

                            option.ResourceGroupName = Environment.GetEnvironmentVariable("ResourceGroupName");
                            option.ServiceBusNamespace = Environment.GetEnvironmentVariable("ServiceBusNamespace");
                            option.ServiceBusNamespaceSASKey = Environment.GetEnvironmentVariable("ServiceBusNamespaceSASKey");
                            option.RequestQueueName = Environment.GetEnvironmentVariable("RequestQueueName");
                            option.ResponseQueueName = Environment.GetEnvironmentVariable("ResponseQueueName");

                            var config = new NLog.Config.LoggingConfiguration();

                            // Targets where to log to: File & Console
                            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "service.log" };
                            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

                            // Rules for mapping loggers to targets            
                            config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logconsole);
                            config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logfile);

                            // Apply config           
                            LogManager.Configuration = config;
                        });

                        var qConfig = new Container(registry).GetInstance<IOptions<AppConfig>>().Value;

                        registry.For<IStartupActivity>()
                        .Use<QueueProbe>()
                        .Ctor<string>("queueName").Is(qConfig.RequestQueueName)
                        .Named(qConfig.RequestQueueName);

                        registry.For<IStartupActivity>()
                        .Use<QueueProbe>()
                        .Ctor<string>("queueName").Is(qConfig.ResponseQueueName)
                        .Named(qConfig.ResponseQueueName);

                        //registry.For<IHostedService>().Use<BackgroundServiceWrapper>();
                        registry.AddHostedService<BackgroundServiceWrapper>();
                        services.AddLamar(registry);
                    })
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                    })
                    .UseNLog();

                // Build and run the host in one go; .RCA is specialized for running it in a console.
                // It registers SIGTERM(Ctrl-C) to the CancellationTokenSource that's shared with all services in the container.
                await builder.Build().RunAsync().ConfigureAwait(false);

                LogManager.GetCurrentClassLogger().Info("The host container has terminated. Press ANY key to exit the console.");
            }
            catch (Exception ex)
            {
                // NLog: catch setup errors (exceptions thrown inside of any containers may not necessarily be caught)
                LogManager.GetCurrentClassLogger().Fatal(ex, "Something went wrong. Application will exit cleanly.");
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
