using AutoMapper;
using Fixit.Core.Storage;
using Fixit.Core.Storage.FileSystem;
using Fixit.Core.Storage.Storage;
using Fixit.FileManagement.Lib.Managers;
using Fixit.FileManagement.Lib.Managers.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Fixit.FileManagement.Lib.Extensions.Managers.Access
{
  public static class AddManagersServices
  {
    public static void AddManagerServices(this IServiceCollection services, bool useAdapter = false)
    {
      if (useAdapter)
      {
        services.AddTransient<IFileSystemManager, FileSystemManager>();
      }
      else
      {
        services.AddTransient<IFileSystemManager, FileSystemManager>(s => new FileSystemManager(s.GetRequiredService<IFileSystemFactory>(), s.GetRequiredService<AzureStorageFactory>(), s.GetRequiredService<IMapper>(), s.GetRequiredService<EventGridTopicServiceClientResolver>(), null));
      }
      services.AddTransient<IFileManager, FileManager>();
      services.AddTransient<IDirectoryManager, DirectoryManager>();
    }
  }
}
