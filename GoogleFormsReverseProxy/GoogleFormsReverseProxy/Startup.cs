using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GoogleFormsReverseProxy
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient();
            services.Configure<GoogleFormsReverseProxyMiddlewareOptions>(options => 
            {
                options.PrepopulatedFormFields.Add("entry.393781397", "Test");
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMiddleware<GoogleFormsReverseProxyMiddleware>();

            app.Run(async (context) =>
            {
                await context.Response.WriteAsync("<a href='/googleforms/d/e/1FAIpQLScnyDaJAcVHlN313kU71bkwHLrevLokbMAFmC2SrwkpELtoOA/viewform?hl=en'>Answer test form</a>");
            });
        }
    }
}
