using System.Threading.Tasks;

namespace ActivemqNet
{
    public interface IConsumer<TMessage> where TMessage : class, new()
    {
        string QueueName { get; }

        Task Consume(TMessage message);
    }
}
