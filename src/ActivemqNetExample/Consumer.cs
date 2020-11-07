using System;
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
        public string QueueName => "queue";

        public async Task Consume(Request message)
        {
            await Task.Delay(500);
            Console.WriteLine(message.Name);
        }
    }
}
