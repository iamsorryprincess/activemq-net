using System;
using System.Linq;
using System.Collections.Generic;

namespace ActivemqNet
{
    public class ActiveMqSettings
    {
        internal string Url { get; private set; } = "tcp://localhost:61616";

        internal string UserName { get; private set; } = "admin";

        internal string Password { get; private set; } = "admin";

        internal int ReconnectionInterval { get; private set; } = 5;

        internal bool IsFullLoggingMessage { get; private set; } = false;
        
        internal List<string> CustomXmlTags { get; private set; } = new List<string>();
        
        internal List<Type> EventHandlers { get; } = new List<Type>();

        internal Dictionary<string, Dictionary<Type, Type>> ConsumerRegistrations { get; } = new Dictionary<string, Dictionary<Type, Type>>();
        
        internal Dictionary<string, List<Type>> ProducerRegistrations { get; } = new Dictionary<string, List<Type>>();

        internal ActiveMqSettings() { }

        public void SetUrl(string url) => Url = url;

        public void SetCredentials(string user, string password)
        {
            UserName = user;
            Password = password;
        }

        public void SetReconnectionInterval(int interval) => ReconnectionInterval = interval;

        public void AddCustomXmlTag(string xmlTag) => CustomXmlTags.Add(xmlTag);

        public void LoggingFullMessage() => IsFullLoggingMessage = true;

        public void RegisterEventHandler<THandler>() where THandler : IEventHandler
        {
            EventHandlers.Clear();
            EventHandlers.Add(typeof(THandler));
        }

        public void AddConsumer<TConsumer>(string queue) where TConsumer : IConsumer
        {
            var type = typeof(TConsumer);
            var interfaceType = type.GetInterfaces()
                                    .FirstOrDefault(a => a.Name.Contains(nameof(IConsumer<object>)))
                                    ?? throw new InvalidOperationException("Cannot get interface type of IConsumer");

            if (!ConsumerRegistrations.ContainsKey(queue))
            {
                ConsumerRegistrations[queue] = new Dictionary<Type, Type>();
            }

            if (!ConsumerRegistrations[queue].Keys.Contains(interfaceType))
            {
                ConsumerRegistrations[queue].Add(interfaceType, type);
            }
        }

        public void AddProducer<TMessage>(string queue) where TMessage : class, new()
        {
            if (!ProducerRegistrations.ContainsKey(queue))
            {
                ProducerRegistrations[queue] = new List<Type>();
            }

            var messageType = typeof(TMessage);
            if (!ProducerRegistrations[queue].Contains(messageType))
            {
                ProducerRegistrations[queue].Add(messageType);
            }
        }
    }
}
