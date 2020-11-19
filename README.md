# ActiveMQNet
[![build](https://github.com/iamsorryprincess/activemq-net/workflows/build/badge.svg)](https://github.com/iamsorryprincess/activemq-net/actions)

### Currently works only with text messages (ITextMessage).
Just define a type that implements IConsumer<Message>, where Message is the class in which instance the incoming message will be deserialized from xml.

## Configuring
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddActiveMq(settings =>
            {
                settings.SetUrl(_configuration["ActiveMQ:Url"]);
                settings.SetCredentials(_configuration["ActiveMQ:UserName"], _configuration["ActiveMQ:Password"]);
                settings.SetReconnectionInterval(1);
                settings.RegisterEventHandler<QueueEventHandler>();
                settings.AddConsumer<Consumer>("queue");
                settings.AddProducer<Request>("out1");
                settings.AddProducer<Request>("out2");
                settings.AddProducer<Request>("out3");
                settings.AddProducer<Request>("out4");
                settings.AddProducer<Request>("out5");
            });
        }
        
## Consumer with message bus which sending message by Type to registered producers
    public class Request
    {
        [XmlElement("name")]
        public string Name { get; set; }
    }

    public class Consumer : IConsumer<Request>
    {
        private readonly MessageBus _bus;

        public Consumer(MessageBus bus)
        {
            _bus = bus;
        }
        
        public async Task Consume(Request message)
        {
            await Task.Delay(500);
            _bus.Publish(message);
        }
    }
    
 ## Event handler for errors and some events like connection
    public class QueueEventHandler : IEventHandler
    {
        private readonly ILogger<QueueEventHandler> _logger;

        public QueueEventHandler(ILogger<QueueEventHandler> logger)
        {
            _logger = logger;
        }
        
        public void HandleError(string error)
        {
            _logger.LogWarning(error);
        }

        public void HandleEvent(string @event)
        {
            _logger.LogInformation(@event);
        }
    }
