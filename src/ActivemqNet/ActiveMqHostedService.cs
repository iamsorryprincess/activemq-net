using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Collections.Generic;
using Apache.NMS;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActivemqNet
{
    internal class ActiveMqHostedService : IHostedService
    {
        private readonly ILogger<ActiveMqHostedService> _logger;
        private readonly ActiveMqSettings _settings;
        private readonly IServiceProvider _serviceProvider;
        private readonly IList<IDisposable> _consumers;
        private readonly IList<IDisposable> _producers;
        private readonly Dictionary<string, Dictionary<Func<string, bool>, Func<string, Task>>> _queueRequestConditions;

        private IConnection _amqConnection;
        private ISession _amqSession;

        public ActiveMqHostedService(ILogger<ActiveMqHostedService> logger, IOptions<ActiveMqSettings> options, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _settings = options.Value;
            _serviceProvider = serviceProvider;
            _consumers = new List<IDisposable>();
            _producers = new List<IDisposable>();
            _queueRequestConditions = new Dictionary<string, Dictionary<Func<string, bool>, Func<string, Task>>>();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartListening();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopListening();
            return Task.CompletedTask;
        }

        private void StartListening()
        {
            var connectionFactory = new NMSConnectionFactory(_settings.Url);
            _amqConnection = connectionFactory.CreateConnection(_settings.UserName, _settings.Password);
            _amqConnection.Start();
            _amqSession = _amqConnection.CreateSession();

            var assembly = Assembly.GetAssembly(typeof(ActiveMqHostedService));
            var types = assembly.GetTypes()
                .Where(x =>
                {
                    var consumerInterface = x.GetInterfaces().FirstOrDefault(a => a.Name.Contains(nameof(IConsumer<object>)));
                    return consumerInterface != null && x.IsClass;
                })
                .SelectMany(x => x.GetInterfaces().Where(a => a.Name.Contains(nameof(IConsumer<object>))))
                .ToList();

            foreach (var type in types)
            {
                var consumeMethod = type.GetMethod(nameof(IConsumer<object>.Consume));
                var generic = type.GetGenericArguments().FirstOrDefault();
                var service = _serviceProvider.GetService(type);
                var queueNamePropertyValue = type.GetProperty(nameof(IConsumer<object>.QueueName))?.GetValue(service) as string;
                var queueName = !string.IsNullOrWhiteSpace(queueNamePropertyValue) ? queueNamePropertyValue : $"{generic.Name}_in";

                if (!_queueRequestConditions.Keys.Contains(queueName))
                {
                    _queueRequestConditions.Add(queueName, new Dictionary<Func<string, bool>, Func<string, Task>>());
                }

                _queueRequestConditions[queueName].Add(msg => msg.Contains(generic.Name), async msg =>
                {
                    var message = DeserializeMessage(msg, generic);

                    if (message != null)
                    {
                        if (consumeMethod.Invoke(service, new[] { message }) is Task consumeTask)
                        {
                            await consumeTask;
                        }
                        else
                        {
                            _logger.LogError("Error when invoke Consume method");
                        }
                    }
                });
            }

            foreach (var queueRequestCondition in _queueRequestConditions)
            {
                var dest = _amqSession.GetQueue(queueRequestCondition.Key);
                var consumer = _amqSession.CreateConsumer(dest);

                consumer.Listener += async message =>
                {
                    if (message is ITextMessage msg && !string.IsNullOrWhiteSpace(msg.Text))
                    {
                        var queueCallback = queueRequestCondition.Value.FirstOrDefault(x => x.Key(msg.Text)).Value;

                        if (queueCallback != null)
                        {
                            await queueCallback.Invoke(msg.Text);
                        }
                        else
                        {
                            _logger.LogError($"Unknown request in queue");
                        }
                    }
                };

                _consumers.Add(consumer);
            }
        }

        private void StopListening()
        {
            _queueRequestConditions.Clear();

            foreach (var consumer in _consumers)
            {
                consumer?.Dispose();
            }

            foreach (var producer in _producers)
            {
                producer?.Dispose();
            }

            _amqSession?.Close();
            _amqConnection?.Stop();
        }

        private object DeserializeMessage(string message, Type messageType)
        {
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(message)))
                {
                    var serializer = new XmlSerializer(messageType);
                    return serializer.Deserialize(ms);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError($"Error while deserializing message. {ex.Message}");
            }

            return null;
        }
    }
}
