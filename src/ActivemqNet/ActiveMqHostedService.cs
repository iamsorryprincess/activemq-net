using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Apache.NMS;
using Apache.NMS.Util;
using Polly;
using Polly.Retry;
using Microsoft.Extensions.Hosting;

namespace ActivemqNet
{
    internal class ActiveMqHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ActiveMqSettings _settings;
        private readonly MessageBus _messageBus;
        private readonly IList<IDisposable> _consumers;
        private readonly IList<IDisposable> _producers;
        private readonly IList<IDisposable> _subscriptions;
        private readonly Dictionary<string, Dictionary<Func<string, bool>, Func<string, Task>>> _queueRequestConditions;
        private readonly RetryPolicy _retryPolicy;

        private IEventHandler _eventHandler;
        private IConnection _amqConnection;
        private ISession _amqSession;

        public ActiveMqHostedService(
            IServiceProvider serviceProvider,
            ActiveMqSettings settings,
            MessageBus messageBus)
        {
            _messageBus = messageBus;
            _settings = settings;
            _serviceProvider = serviceProvider;
            _consumers = new List<IDisposable>();
            _producers = new List<IDisposable>();
            _subscriptions = new List<IDisposable>();
            _queueRequestConditions = new Dictionary<string, Dictionary<Func<string, bool>, Func<string, Task>>>();
            _retryPolicy = Policy
                .Handle<NMSException>()
                .WaitAndRetryForever(retryAttempt => TimeSpan.FromMinutes(_settings.ReconnectionInterval), OnRetry);
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
            _eventHandler = _serviceProvider.GetService(typeof(IEventHandler)) as IEventHandler;
            
            _retryPolicy.Execute(() =>
            {
                var connectionFactory = new NMSConnectionFactory(_settings.Url);
                _amqConnection = connectionFactory.CreateConnection(_settings.UserName, _settings.Password);
            });
            
            _amqConnection.ExceptionListener += AmqConnectionOnException;
            _amqConnection.Start();
            _amqSession = _amqConnection.CreateSession();
            _eventHandler?.HandleEvent($"Connected to {_settings.Url}");
            RegisterProducers();
            RegisterConsumers();
        }

        private void StopListening()
        {
            _queueRequestConditions.Clear();

            foreach (var consumer in _consumers)
            {
                consumer?.Dispose();
            }

            foreach (var subscription in _subscriptions)
            {
                subscription?.Dispose();
            }

            foreach (var producer in _producers)
            {
                producer?.Dispose();
            }

            _subscriptions.Clear();
            _amqSession?.Close();
            _amqConnection?.Stop();
        }
        
        private void AmqConnectionOnException(Exception exception)
        {
            _eventHandler?.HandleError(exception.Message);
            StopListening();
            StartListening();
        }
        
        private void OnRetry(Exception exception, TimeSpan interval)
        {
            _eventHandler?.HandleError(exception.Message);
        }

        private void RegisterProducers()
        {
            foreach (var (queueName, types) in _settings.ProducerRegistrations)
            {
                var dest = SessionUtil.GetQueue(_amqSession, queueName);
                var producer = _amqSession.CreateProducer(dest);
                var subscription = _messageBus.MessageStream.Subscribe(message =>
                {
                    if (!types.Contains(message.GetType())) return;
                    var serializedMessage = Serializer.SerializeMessage(message);

                    if (serializedMessage == null)
                    {
                        _eventHandler?.HandleError($"Error while serializing message {message.GetType()} in queue {queueName}");
                        return;
                    }
                    
                    var msg = producer.CreateTextMessage(serializedMessage);
                    producer.Send(msg);
                });
                
                _subscriptions.Add(subscription);
                _producers.Add(producer);
                _eventHandler?.HandleEvent($"Producer created for queue {queueName}");
            }
        }

        private void RegisterConsumers()
        {
            foreach (var (queueName, consumerRegistrations) in _settings.ConsumerRegistrations)
            {
                var dest = _amqSession.GetQueue(queueName);
                var consumer = _amqSession.CreateConsumer(dest);
                _consumers.Add(consumer);
                _eventHandler?.HandleEvent($"Start listening queue {queueName}");

                if (!_queueRequestConditions.Keys.Contains(queueName))
                {
                    _queueRequestConditions.Add(queueName, new Dictionary<Func<string, bool>, Func<string, Task>>());
                }

                consumer.Listener += async message =>
                {
                    switch (message)
                    {
                        case ITextMessage msg when !string.IsNullOrWhiteSpace(msg.Text):
                        {
                            if (_settings.IsFullLoggingMessage)
                            {
                                _eventHandler?.HandleEvent($"Message from queue {queueName}: {msg.Text}");
                            }
                            
                            var queueCallback = _queueRequestConditions[queueName]
                                .FirstOrDefault(x => x.Key.Invoke(msg.Text)).Value;

                            if (queueCallback == null)
                            {
                                _eventHandler?.HandleError($"Unknown request in queue {queueName}");
                                return;
                            }
                            
                            await queueCallback.Invoke(msg.Text);
                            break;
                        }

                        default:
                            _eventHandler?.HandleError($"Unknown message type (${message?.GetType().Name}) in queue {queueName}");
                            break;
                    }
                };

                foreach (var consumerType in consumerRegistrations.Keys)
                {
                    var consumeMethod = consumerType.GetMethod(nameof(IConsumer<object>.Consume));
                    var generic = consumerType.GetGenericArguments().FirstOrDefault();
                    var service = _serviceProvider.GetService(consumerType);

                    _queueRequestConditions[queueName].Add(
                        msg => msg.Contains($"<{generic?.Name}") || _settings.CustomXmlTags.Any(xmlTag => msg.Contains($"<{xmlTag}:{generic?.Name}")),
                        async msg =>
                    {
                        var message = Serializer.DeserializeMessage(msg, generic);

                        if (message == null)
                        {
                            _eventHandler?.HandleError($"Error while deserializing message from queue {queueName}");
                            return;
                        }
                        
                        if (consumeMethod?.Invoke(service, new[] {message}) is Task consumeTask)
                        {
                            await consumeTask;
                        }
                        else
                        {
                            _eventHandler?.HandleError($"Cannot invoke consume method from consumer {service.GetType().Name}. Queue - {queueName}");
                        }
                    });
                }
            }
        }
    }
}
