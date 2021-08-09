using AutoMapper;
using Fixit.FileManagement.Lib;
using Fixit.FileManagement.Lib.Adapters;
using Fixit.FileManagement.Lib.Extensions.Managers.Access;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System.Collections.Generic;
using Fixit.Core.DataContracts.Events;
using Fixit.Core.Storage.DataContracts.FileSystem.EventDefinitions;
using Fixit.Core.Storage.FileSystem.Adapters;
using Fixit.Core.Storage.FileSystem.Extensions;
using Fixit.Core.Storage.FileSystem.Managers;
using Fixit.Core.Storage.FileSystem.Resolvers;
using Fixit.Core.Storage.Storage.Blob.Adapters;
using Fixit.Core.Storage.Storage.Extensions;

namespace Fixit.FileManagement.WebApi
{
  public class Startup
  {
    private readonly string _urlRegex = @"^(ht|f)tp(s?)\:\/\/[0-9a-zA-Z]([-.\w]*[0-9a-zA-Z])*(:(0-9)*)*(\/?)([a-zA-Z0-9\-\.\?\,\'\/\\\+&%\$#_]*)?$";

    public Startup(IConfiguration configuration)
    {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
      services.AddApplicationInsightsTelemetry();
      services.AddAutoMapper();
      services.AddMemoryCache();
      services.AddFixitCoreStorageServices();
      services.AddFixitCoreFileSystemServices();

      services.AddSingleton<EventGridTopicServiceClientResolver>(serviceProvider => key =>
      {
        var topicEndpoint = string.Empty;
        var topicKey = string.Empty;

        if (key == FileEventDefinitions.RegenerateImageUrl.ToString())
        {
          topicEndpoint = Configuration["FMSEventGrid:FIXIT-FMS-EG-ONIMAGEEXPIRED-TE"];
          topicKey = Configuration["FMSEventGrid:FIXIT-FMS-EG-ONIMAGEEXPIRED-TK"];
          return AzureEventGridTopicServiceClientFactory.CreateEventGridTopicServiceClient(topicEndpoint, topicKey);
        }
        else if (key == FileEventDefinitions.ImageUrlsUpdate.ToString())
        {
          topicEndpoint = Configuration["FMSEventGrid:FIXIT-FMS-EG-ONIMAGEURLSUPDATE-TE"];
          topicKey = Configuration["FMSEventGrid:FIXIT-FMS-EG-ONIMAGEURLSUPDATE-TK"];
          return AzureEventGridTopicServiceClientFactory.CreateEventGridTopicServiceClient(topicEndpoint, topicKey);
        }
        else
        {
          throw new KeyNotFoundException();
        }
      });
      
      services.AddTransient<FileSystemResolvers.FileSystemClientResolver>(services => (dataLakeFileSystemAdapter,blobStorageClientAdapter, mapper) =>
      {
        return new FileSystemClientAdapter(dataLakeFileSystemAdapter, blobStorageClientAdapter, mapper, services.GetRequiredService<IConfiguration>() ,services.GetRequiredService<EventGridTopicServiceClientResolver>());
      });

      services.AddManagerServices(true);    


      var securityScheme = new OpenApiSecurityScheme
      {
        Description = "Enter JWT Bearer token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer",
        Reference = new OpenApiReference
        {
          Type = ReferenceType.SecurityScheme,
          Id = JwtBearerDefaults.AuthenticationScheme
        }
      };

      services.AddSwaggerGen(c =>
      {
        c.SwaggerDoc("v1", new OpenApiInfo());
        c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
          {securityScheme, new string[] { } }
        });
      });

      services.Configure<FormOptions>(x =>
      {
        x.ValueLengthLimit = int.MaxValue;
        x.MultipartBodyLengthLimit = int.MaxValue;
        x.MultipartHeadersLengthLimit = int.MaxValue;
      });

      services.AddDataProtection();
      services.AddControllers();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      if (env.IsDevelopment() || env.IsEnvironment("local"))
      {
        app.UseDeveloperExceptionPage();
      }

      app.UseHttpsRedirection();
      app.UseRouting();

      app.UseAuthentication();
      app.UseAuthorization();
      app.UseStaticFiles();

      app.UseSwagger();
      app.UseSwaggerUI(c =>
      {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fixit.FMS API");
      });

      app.UseEndpoints(endpoints =>
      {
        endpoints.MapControllerRoute(
          name: "default",
          pattern: "{controller=Home}/{action=Index}/{id?}");
      });
    }
  }
}
