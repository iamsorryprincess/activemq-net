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
            services.AddActiveMQ(_configuration.GetSection("ActiveMQ"));
        }

        public void Configure(IApplicationBuilder app)
        {
        }
    }
}
