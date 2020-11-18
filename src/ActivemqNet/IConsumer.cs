using System.Threading.Tasks;

namespace ActivemqNet
{
    public interface IConsumer { }

    public interface IConsumer<in TMessage> : IConsumer where TMessage : class, new()
    {
        Task Consume(TMessage message);
    }
}
