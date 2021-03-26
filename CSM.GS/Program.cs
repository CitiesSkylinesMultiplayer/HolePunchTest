using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CSM.GS
{
    /// <summary>
    ///     This is the UDP hole punching server, the clients will connect
    ///     to this server to setup the correct ports
    /// </summary>
    public class Program
    {
        public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();
        
        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                })
                .ConfigureServices((hostContext, services) => 
                    services.AddHostedService<WorkerService>());
    }
}