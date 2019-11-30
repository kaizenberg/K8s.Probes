using Kubernetes.Probes.Core;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kubernetes.Probes.Worker
{
    public class ServiceImplementation : IServiceImplementation
    {
        private readonly ILogger<ServiceImplementation> _logger;
        private readonly AppConfig _config;
        private MessageReceiver _receiver;
        private TaskCompletionSource<bool> _doneReceiving;

        public ServiceImplementation(ILogger<ServiceImplementation> logger, IOptions<AppConfig> config)
        {
            _logger = logger;
            _config = config.Value;
            _doneReceiving = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            _logger.LogInformation($"Worker started at {DateTimeOffset.Now}");
            await ReceiveMessagesAsync(_config.ServiceBusNamespaceSASKey, _config.RequestQueueName, token);
        }

        private async Task ReceiveMessagesAsync(string connectionString, string queueName, CancellationToken cancellationToken)
        {
            // Do not retry communicating with the service bus if there are issues with the connection
            _receiver = new MessageReceiver(connectionString, queueName, ReceiveMode.PeekLock, RetryPolicy.NoRetry);

            cancellationToken.Register(async () =>
            {
                await _receiver.CloseAsync().ConfigureAwait(false);
            });

            // Register the RegisterMessageHandler callback
            _receiver.RegisterMessageHandler(
                MessageHandlerCallback,
                new MessageHandlerOptions(LogMessageHandlerException)
                {
                    AutoComplete = false,
                    MaxConcurrentCalls = 1,
                    MaxAutoRenewDuration = TimeSpan.FromMinutes(5)
                });

            // Await on the task so that caller see that the method doesn't run to completion
            await _doneReceiving.Task;
        }

        private async Task MessageHandlerCallback(Message message, CancellationToken cancellationToken1)
        {
            if (!_receiver.IsClosedOrClosing &&
                message.Label != null &&
                message.ContentType != null &&
                message.Label.Equals("Scientist", StringComparison.InvariantCultureIgnoreCase) &&
                message.ContentType.Equals("application/json", StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogInformation($"Received MessageId = {message.MessageId}");

                // Fake deay to mimic long running job
                await Task.Delay(10000).ConfigureAwait(false);

                // Following error doesn't get bubbled up to BackgroundServiceWraper class. Message Recevier never closes automatically.
                throw new Exception();

                // Send a copy of this message to the response queue
                await this.SendMessagesAsync(_config.ServiceBusNamespaceSASKey, _config.ResponseQueueName, message.Clone(), cancellationToken1);

                // Mark the orginal message as complete so that it is removed from the queue
                await _receiver.CompleteAsync(message.SystemProperties.LockToken);
            }
            else
            {
                await _receiver.DeadLetterAsync(message.SystemProperties.LockToken);
            }
        }

        private Task LogMessageHandlerException(ExceptionReceivedEventArgs e)
        {
            _logger.LogInformation($"Queue Name: {e.ExceptionReceivedContext.EntityPath}, {e.Exception.ToString()}");

            // Set status on the Task Completion source so that caller can take necessary actions. 
            // In this case, caller cancels parent token which triggers closure of the Message Receiver.
            // This is only to demonstrate how to close Message Receiver when using RegisterMessageHandler. (Receive-loop is ideal & recommended way)
            // Otherwise MessageReceivePump swallows exception thrown in this method. 
            // Although design-wise this is correct but technically inconvenient because it doesn't provide any means to exit message processing.
            _doneReceiving.TrySetResult(false);

            return Task.CompletedTask;
        }

        private async Task SendMessagesAsync(string connectionString, string queueName, Message msgCopy, CancellationToken cancellationToken)
        {
            var sender = new MessageSender(connectionString, queueName);
            await sender.SendAsync(msgCopy).ConfigureAwait(false);
            _logger.LogInformation($"Sent MessageId = {msgCopy.MessageId} to {queueName}");
        }
    }
}
