using System;

namespace NetStandardClientSender
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Core;
    using Newtonsoft.Json;

    class Program
    {
        static async Task Main(string[] args)
        {
            var topicClient = new TopicClient("Endpoint=sb://asb-metrics-test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=mi7Xv/5aQk5pv3m6W+GIjuPMgiVXrapzjdm9ovV5onE=", "test");

            topicClient.RegisterPlugin(new MetricsPlugin());

            Console.Write("Batch size: ");
            var batchSize = int.Parse(Console.ReadLine());

            while (true)
            {
                var messages = new List<Message>();
                for (int i = 0; i < batchSize; i++)
                {
                    var message = new Message();
                    var body = new OrderAccepted(Guid.NewGuid().ToString());
                    message.Body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));
                    message.ContentType = "application/json";
                    message.Label = body.GetType().Name;

                    messages.Add(message);            
                }
                await topicClient.SendAsync(messages);
                Console.WriteLine($"Sent {batchSize} messages");
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }

    public class OrderAccepted
    {
        public OrderAccepted(string orderReference)
        {
            OrderReference = orderReference;
        }

        public string OrderReference { get; }
    }

    public class MetricsPlugin : ServiceBusPlugin
    {
        public override string Name => "ASOS Metric";

        public override Task<Message> BeforeMessageSend(Message message)
        {
            message.UserProperties.Add("DateTimeSent", DateTimeOffset.UtcNow);

            return base.BeforeMessageSend(message);
        }
    }
}
