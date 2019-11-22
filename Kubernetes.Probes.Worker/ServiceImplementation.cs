using Kubernetes.Probes.Core;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kubernetes.Probes.Worker
{
    public class ServiceImplementation : IServiceImplementation
    {
        private readonly ILogger<ServiceImplementation> _logger;
        private readonly AppConfig _config;

        public ServiceImplementation(ILogger<ServiceImplementation> logger, IOptions<AppConfig> config)
        {
            _logger = logger;
            _config = config.Value;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            _logger.LogInformation($"Worker started at {DateTimeOffset.Now}");

            //Send dumy messages to the request queue
            await this.SendMessagesAsync(_config.RequestQueueConnectionString, _config.RequestQueue, token).ConfigureAwait(false);

            //Start receiving those messages
            await this.ReceiveMessagesAsync(_config.RequestQueueConnectionString, _config.RequestQueue, token);
        }

        private async Task ReceiveMessagesAsync(string connectionString, string queueName, CancellationToken cancellationToken)
        {
            var receiver = new MessageReceiver(connectionString, queueName, ReceiveMode.PeekLock);

            var doneReceiving = new TaskCompletionSource<bool>();
            // close the receiver and factory when the CancellationToken fires 
            cancellationToken.Register(
                async () =>
                {
                    await receiver.CloseAsync();
                    doneReceiving.SetResult(true);
                });

            // register the RegisterMessageHandler callback
            receiver.RegisterMessageHandler(
                async (message, cancellationToken1) =>
                {
                    if (message.Label != null &&
                        message.ContentType != null &&
                        message.Label.Equals("Scientist", StringComparison.InvariantCultureIgnoreCase) &&
                        message.ContentType.Equals("application/json", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var body = message.Body;

                        dynamic scientist = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(body));

                        _logger.LogInformation($"Received MessageId = {message.MessageId} with Body = {scientist.name}");

                        await receiver.CompleteAsync(message.SystemProperties.LockToken);

                        //Send a copy of this message to the response queue
                        await this.SendMessagesAsync(_config.ResponseQueueConnectionString, _config.ResponseQueue, cancellationToken);
                    }
                    else
                    {
                        await receiver.DeadLetterAsync(message.SystemProperties.LockToken); //, "ProcessingError", "Don't know what to do with this message");
                    }
                },
                new MessageHandlerOptions((e) => LogMessageHandlerException(e)) { AutoComplete = false, MaxConcurrentCalls = 1 });

            await doneReceiving.Task;
        }

        private Task LogMessageHandlerException(ExceptionReceivedEventArgs e)
        {
            _logger.LogInformation($"Exception: {e.Exception.Message} for {e.ExceptionReceivedContext.EntityPath}");
            return Task.CompletedTask;
        }

        private async Task SendMessagesAsync(string connectionString, string queueName, CancellationToken cancellationToken)
        {
            var sender = new MessageSender(connectionString, queueName);

            dynamic data = new
            {
                name = "CV Raman",
            };

            var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data)))
            {
                ContentType = "application/json",
                Label = "Scientist",
                MessageId = "0",
                TimeToLive = TimeSpan.FromMinutes(2)
            };

            await sender.SendAsync(message);

            //uncomment below line to demostrate how errors in the service lead to discontinuation of alive.txt file creation that ultimately leeds to pod restart.
            //throw new UnauthorizedException("Fake authorization error orccured");

            _logger.LogInformation($"Sent MessageId = {message.MessageId} to {queueName}");
        }
    }
}
