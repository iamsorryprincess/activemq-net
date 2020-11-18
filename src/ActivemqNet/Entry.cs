using System;
using Microsoft.Extensions.DependencyInjection;

namespace ActivemqNet
{
    public static class Entry
    {
        public static IServiceCollection AddActiveMq(this IServiceCollection services, Action<ActiveMqSettings> setupAction)
        {
            var settings = new ActiveMqSettings();
            setupAction.Invoke(settings);

            foreach (var registrations in settings.ConsumerRegistrations.Values)
            {
                foreach (var (consumer, implementation) in registrations)
                {
                    services.AddTransient(consumer, implementation);
                }
            }

            if (settings.EventHandlers.Count > 0)
            {
                var handlerImplementation = settings.EventHandlers[0];
                services.AddTransient(typeof(IEventHandler), handlerImplementation);
            }
            
            services.AddSingleton(settings);
            services.AddSingleton<MessageBus>();
            services.AddHostedService<ActiveMqHostedService>();
            return services;
        }
    }
}
