using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ActivemqNet
{
    public static class Entry
    {
        public static IServiceCollection AddActiveMQ(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.Configure<ActiveMqSettings>(configurationSection);
            var assembly = Assembly.GetAssembly(typeof(ActiveMqHostedService));
            var types = assembly.GetTypes()
                .Where(x =>
                {
                    var consumerInterface = x.GetInterfaces().FirstOrDefault(a => a.Name.Contains(nameof(IConsumer<object>)));
                    return consumerInterface != null && x.IsClass;
                })
                .ToDictionary(key => key.GetInterfaces().FirstOrDefault(a => a.Name.Contains(nameof(IConsumer<object>))), value => value);

            foreach (var type in types)
            {
                services.AddTransient(type.Key, type.Value);
            }

            services.AddHostedService<ActiveMqHostedService>();
            return services;
        }
    }
}
