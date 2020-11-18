using ActivemqNet;
using Microsoft.Extensions.Logging;

namespace ActivemqNetExample
{
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
}