using System;

namespace NetStandardClientEndpoint
{
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Core;

    class Program
    {
        static void Main(string[] args)
        {
            var telemetryClient = new TelemetryClient(new TelemetryConfiguration("f9bdad81-154c-4b5a-bf38-f0fee49ed358"));

            var subscriptionClient = new SubscriptionClient(
                "Endpoint=sb://asb-metrics-test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=mi7Xv/5aQk5pv3m6W+GIjuPMgiVXrapzjdm9ovV5onE=",
                "test",
                "test-subscription");

            var pipeline = new MessagePipeline(new MetricsFilter(TimeSpan.FromSeconds(5), telemetryClient), new MessageHandler());

            // client.RegisterPlugin(new MetricsPlugin());

            // Read x messages at a time - default is 0 (turned off) - ignored by the RegisterMessageHandler pump
            // See - https://github.com/Azure/azure-service-bus-dotnet/issues/582
            subscriptionClient.PrefetchCount = 100; 
            subscriptionClient.RegisterMessageHandler(async (message, ctx) =>
                {
                    await pipeline.Process(message);
                },
                new MessageHandlerOptions(e => // Exception handler is called when the handler or plugin fails
                {
                    // TODO: Figure out how to best plug this into a pipeline
                    Console.WriteLine($"Exception thrown - {e.Exception.Message}");
                    return Task.CompletedTask;
                }) { AutoComplete = true});

            Console.ReadLine();
        }
    }

    public class MetricsFilter : IMessageHandlerFilter
    {
        private readonly TimeSpan sla;
        private readonly TelemetryClient telemetryClient;

        public MetricsFilter(TimeSpan sla, TelemetryClient telemetryClient)
        {
            this.sla = sla;
            this.telemetryClient = telemetryClient;
        }

        public async Task Handle(Message message, IMessageHandler next)
        {
            var processingStartTime = DateTimeOffset.UtcNow;

            await next.Handle(message);

            var dateSent = (DateTimeOffset)message.UserProperties["DateTimeSent"];
            var processingEndTime = DateTimeOffset.UtcNow;

            var processingTime = processingEndTime - processingStartTime;
            var criticalTime = processingEndTime - dateSent;
            var timeToExceedSLA = sla - criticalTime;

            telemetryClient.GetMetric("Processing Time").TrackValue(processingTime.TotalSeconds);
            telemetryClient.GetMetric("Critical Time").TrackValue(criticalTime.TotalSeconds);
            telemetryClient.GetMetric("Time to exceed SLA").TrackValue(timeToExceedSLA.TotalSeconds);
        }
    }

    public class MessageHandler : IMessageHandler
    {
        public Task Handle(Message message)
        {
            Console.WriteLine($"Processing message - {message.MessageId}");
            return Task.CompletedTask;
        }
    }


    public class MessagePipeline
    {
        private readonly IMessageHandlerFilter filter; // TODO: Would allow a chain of filters ideally
        private readonly IMessageHandler handler; // TODO: Would ideally allow you to register a handler based on message type

        public MessagePipeline(IMessageHandlerFilter filter, IMessageHandler handler)
        {
            this.filter = filter;
            this.handler = handler;
        }

        public async Task Process(Message message)
        {
            await filter.Handle(message, this.handler);
        }
    }

    public interface IMessageHandlerFilter
    {
        Task Handle(Message message, IMessageHandler next);
    }

    public interface IMessageHandler
    {
        Task Handle(Message message);
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

        public override Task<Message> AfterMessageReceive(Message message)
        {
            DateTimeOffset dateTimeSent = (DateTimeOffset)message.UserProperties["DateTimeSent"];

            Console.WriteLine($"Time message sent {dateTimeSent} - id {message.MessageId}");

            return base.AfterMessageReceive(message);
        }

        public override Task<Message> BeforeMessageSend(Message message)
        {
            message.UserProperties.Add("DateTimeSent", DateTimeOffset.UtcNow);

            return base.BeforeMessageSend(message);
        }
    }
}
