using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Z1RSignalRHost
{
    public class Startup
    {
        public static string PublicHost = "localhost"; // override via UI or config

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR();
            services.AddControllers();
            // The web tracker runs on another origin, so the browser needs CORS to reach this relay. Desktop trackers send no Origin and are not affected.
            services.AddCors(o => o.AddPolicy("webTracker", p => p
                .SetIsOriginAllowed(origin =>
                {
                    if (!System.Uri.TryCreate(origin, System.UriKind.Absolute, out var u)) return false;
                    return u.Host == "zoratracker.gg" || u.Host.EndsWith(".zoratracker.gg") || u.Host == "localhost" || u.Host == "127.0.0.1";
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            // 🆕 Add this BEFORE UseRouting
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
            });

            app.UseRouting();
            app.UseCors("webTracker");

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<SyncHub>("/hub");
            });
        }
    }
}
