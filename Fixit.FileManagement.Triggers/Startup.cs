using AutoMapper;
using Fixit.FileManagement.Lib;
using Fixit.FileManagement.Lib.Adapters;
using Fixit.FileManagement.Lib.Extensions.Managers.Access;
using Fixit.FileManagement.Lib.Mappers;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using Fixit.Core.DataContracts.Events;
using Fixit.Core.Storage.DataContracts.FileSystem.EventDefinitions;
using Fixit.Core.Storage.FileSystem;
using Fixit.Core.Storage.FileSystem.Mappers;
using Fixit.Core.Storage.FileSystem.Resolvers;
using Fixit.Core.Storage.Storage;

[assembly: FunctionsStartup(typeof(Fixit.BillingManagement.Triggers.Startup))]
namespace Fixit.BillingManagement.Triggers
{
  class Startup : FunctionsStartup
  {
    private IConfiguration _configuration;

    public override void Configure(IFunctionsHostBuilder builder)
    {
      _configuration = (IConfiguration)builder.Services.BuildServiceProvider()
                                                       .GetService(typeof(IConfiguration));

      builder.Services.AddSingleton<IMapper>(provider =>
      {
        var mapperConfig = new MapperConfiguration(mc => { 
          mc.AddProfile(new FileManagementMapper()); 
          mc.AddProfile(new FileSystemDtoMapper()); 
        });
        return mapperConfig.CreateMapper();
      });  
      builder.Services.AddSingleton<IStorageFactory, AzureStorageFactory>(provider =>
      {
        var configuration = provider.GetService<IConfiguration>();

        var name = configuration["FIXIT-FMS-SA-AN"];
        var key = configuration["FIXIT-FMS-SA-AK"];
        var uri = configuration["FIXIT-FMS-SA-EP"];

        return new AzureStorageFactory(provider.GetService<IMapper>(), name, key, uri);
      });

      builder.Services.AddSingleton<IFileSystemFactory, FileSystemFactory>(provider =>
      {
        var storageFactory = provider.GetService<IStorageFactory>();
        var configuration = provider.GetService<IConfiguration>();

        var name = configuration["FIXIT-FMS-SA-AN"];
        var key = configuration["FIXIT-FMS-SA-AK"];
        var uri = configuration["FIXIT-FMS-SA-EP"];

        return new FileSystemFactory(provider.GetService<IMapper>(), storageFactory, name, key, uri);
      });

      builder.Services.AddTransient<EventGridTopicServiceClientResolver>(serviceProvider => key =>
      {
        var topicEndpoint = string.Empty;
        var topicKey = string.Empty;

        if (key == FileEventDefinitions.RegenerateImageUrl.ToString())
        {
          topicEndpoint = _configuration["FIXIT-FMS-EG-ONIMAGEEXPIRED-TE"];
          topicKey = _configuration["FIXIT-FMS-EG-ONIMAGEEXPIRED-TK"];
          return AzureEventGridTopicServiceClientFactory.CreateEventGridTopicServiceClient(topicEndpoint, topicKey);
        }
        else if (key == FileEventDefinitions.ImageUrlsUpdate.ToString())
        {
          topicEndpoint = _configuration["FIXIT-FMS-EG-ONIMAGEURLSUPDATE-TE"];
          topicKey = _configuration["FIXIT-FMS-EG-ONIMAGEURLSUPDATE-TK"];
          return AzureEventGridTopicServiceClientFactory.CreateEventGridTopicServiceClient(topicEndpoint, topicKey);
        }
        else
        {
          throw new KeyNotFoundException();
        }
      });

      builder.Services.AddTransient<FileSystemResolvers.FileSystemClientResolver>(services => (dataLakeFileSystemAdapter, blobStorageClientAdapter, mapper) =>
      {
        return new FileSystemClientAdapter(dataLakeFileSystemAdapter, blobStorageClientAdapter, mapper, services.GetRequiredService<IConfiguration>(), services.GetRequiredService<EventGridTopicServiceClientResolver>());
      });

      builder.Services.AddManagerServices();
    }
  }
}
