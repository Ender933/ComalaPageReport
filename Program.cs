using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace ComalaPageReport
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Comala Page Report ===");
            Console.WriteLine();

            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            var settings = new AppSettings();
            config.GetSection("Confluence").Bind(settings);

            if (string.IsNullOrEmpty(settings.BaseUrl) ||
                string.IsNullOrEmpty(settings.Username) ||
                string.IsNullOrEmpty(settings.ApiToken) ||
                string.IsNullOrEmpty(settings.SpaceKey))
            {
                Console.WriteLine("ERROR: Bitte appsettings.json mit den Confluence-Zugangsdaten befüllen.");
                return;
            }

            Console.WriteLine($"Instanz:    {settings.BaseUrl}");
            Console.WriteLine($"Space:      {settings.SpaceKey}");
            Console.WriteLine($"Ausgabe:    {settings.OutputPath}");
            Console.WriteLine();

            var service = new ReportService(settings);
            await service.RunAsync();
        }
    }
}
