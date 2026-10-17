using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;


namespace Z1RSignalRHost
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string port = "5000"; // default

            // Try to get a port from args[0], fallback to 5000
            if (args.Length > 0 && int.TryParse(args[0], out int parsedPort) && parsedPort >= 1024 && parsedPort <= 65535)
                port = parsedPort.ToString();
            CreateHostBuilder(port).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string port) =>
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.Limits.MinRequestBodyDataRate = null;
                    });

                    // Use the dynamic port here
                    webBuilder.UseUrls($"http://0.0.0.0:{port}");
                    webBuilder.UseStartup<Startup>();
                });
    }
}
