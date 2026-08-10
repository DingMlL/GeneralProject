using GeneralProject.Transport.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GeneralProject.UIDemo
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddTransport();
                    services.AddScoped<Form1>();
                })
                .Build();

            var form1 = host.Services.GetRequiredService<Form1>();
            Application.Run(form1);
        }
    }
}