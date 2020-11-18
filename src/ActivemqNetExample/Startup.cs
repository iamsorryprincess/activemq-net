using ActivemqNet;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ActivemqNetExample
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddActiveMq(settings =>
            {
                settings.SetUrl(_configuration["ActiveMQ:Url"]);
                settings.SetCredentials(_configuration["ActiveMQ:UserName"], _configuration["ActiveMQ:Password"]);
                settings.SetReconnectionInterval(1);
                settings.RegisterEventHandler<QueueEventHandler>();
                settings.AddConsumer<Consumer>("queue");
                settings.AddProducer<Request>("out1");
                settings.AddProducer<Request>("out2");
                settings.AddProducer<Request>("out3");
                settings.AddProducer<Request>("out4");
                settings.AddProducer<Request>("out5");
            });
        }

        public void Configure(IApplicationBuilder app)
        {
        }
    }
}
