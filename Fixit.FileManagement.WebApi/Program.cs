using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Fixit.FileManagement.WebApi
{
  public class Program
  {
    public static void Main(string[] args)
    {
      CreateWebHostBuilder(args).Build().Run();
    }

    public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>

      WebHost.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
          var wConfiguration = config.Build();
          var wKeyvaultUri = wConfiguration["KeyVault:KeyVaultUri"];

          if (!string.IsNullOrEmpty(wKeyvaultUri))
          {
            config.AddAzureKeyVault(wKeyvaultUri,
              wConfiguration["KeyVault:ClientId"],
              wConfiguration["KeyVault:ClientSecret"]);
          }
        })
        .UseStartup<Startup>();
  }
}
