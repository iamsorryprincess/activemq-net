using System.Threading.Tasks;
using System.Xml.Serialization;
using ActivemqNet;

namespace ActivemqNetExample
{
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
}
