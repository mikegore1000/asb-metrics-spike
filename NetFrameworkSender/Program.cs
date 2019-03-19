namespace NetFrameworkSender
{
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    class Program
    {
        static void Main(string[] args)
        {
            var mf = MessagingFactory.CreateFromConnectionString(
                "Endpoint=sb://asb-metrics-test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=mi7Xv/5aQk5pv3m6W+GIjuPMgiVXrapzjdm9ovV5onE=");

            var sub = mf.CreateSubscriptionClient("test", "test-subscription");

            // NOTE: All you can do with the subscription client is create a basic message pump.  You have to create a handler pipeline
        }
    }
}
