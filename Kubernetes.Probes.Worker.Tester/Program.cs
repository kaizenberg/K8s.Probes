using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using System;
using System.Threading;

namespace Kubernetes.Probes.Worker.Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            var sender = new MessageSender(Environment.GetEnvironmentVariable("QueueConnectionString"), Environment.GetEnvironmentVariable("QueueName"));
            var rnd = new Random();

            while (true)
            {
                byte[] bufffer = new byte[100];
                rnd.NextBytes(bufffer);
                var msgId = Guid.NewGuid().ToString();
                Console.WriteLine($"Sending message with id {msgId}");

                var message = new Message(bufffer)
                {
                    ContentType = "application/json",
                    Label = "Scientist",
                    MessageId = Guid.NewGuid().ToString(),
                };

                sender.SendAsync(message).GetAwaiter().GetResult();
                Console.WriteLine($"Sent. Kill the program before it sends another.");
            }
        }
    }
}
