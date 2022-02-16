using AutoMapper;
using Fixit.Core.DataContracts;
using Fixit.FileManagement.Lib.Models;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Fixit.Core.Storage.DataContracts.FileSystem.Files;
using Fixit.Core.Storage.DataContracts.TableEntities;
using Fixit.Core.Storage.FileSystem;
using Fixit.Core.Storage.FileSystem.Managers;
using Fixit.Core.Storage.FileSystem.Resolvers;
using Fixit.Core.Storage.Storage;
using Fixit.Core.Storage.Storage.Table.Managers;

[assembly: InternalsVisibleTo("Fixit.FileManagement.WebApi.UnitTests")]
namespace Fixit.FileManagement.Lib.Managers.Internal
{
  internal class FileSystemManager : IFileSystemManager
  {
    private readonly IFileSystemServiceClient _fileSystemServiceClient;
    private readonly ITableServiceClient _tableServiceClient;
    private readonly IMapper _mapper;
    private readonly EventGridTopicServiceClientResolver _eventGridTopicServiceClientResolver;  

    public FileSystemManager(IFileSystemFactory fileSystemFactory,
                             IStorageFactory storageFactory,
                             IMapper mapper,
                             EventGridTopicServiceClientResolver eventGridTopicServiceClientResolver,
                             FileSystemResolvers.FileSystemClientResolver fileSystemClientResolver)
    {

      _mapper = mapper ?? throw new ArgumentNullException($"{nameof(FileSystemManager)} expects a value for {nameof(mapper)}... null argument was provided");
      _eventGridTopicServiceClientResolver = eventGridTopicServiceClientResolver ?? throw new ArgumentNullException($"{nameof(FileSystemManager)} expects a value for {nameof(eventGridTopicServiceClientResolver)}... null argument was provided");
      _ = fileSystemFactory ?? throw new ArgumentNullException($"{nameof(FileSystemManager)} expects a value for {nameof(fileSystemFactory)}... null argument was provided");
      _ = storageFactory ?? throw new ArgumentNullException($"{nameof(FileSystemManager)} expects a value for {nameof(storageFactory)}... null argument was provided");

      _fileSystemServiceClient = fileSystemFactory.CreateDataLakeFileSystemServiceClient(fileSystemClientResolver);
      _tableServiceClient = storageFactory.CreateTableStorageClient();
    }

    #region Create File Systems
    public async Task<FileSystemCreate> CreateOrGetFileSystemAsync(string fileSystemName, string fileSystemId, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException(nameof(fileSystemName)) : string.Empty;     

      var result = new FileSystemCreate();

      result.FileSystemClient = await _fileSystemServiceClient.CreateOrGetFileSystemAsync(fileSystemName, cancellationToken);
      if (result.FileSystemClient != null)
      {         
        var directoryCreation = await result.FileSystemClient.CreateDirectoryIfNotExistsAsync(fileSystemId.ToString(), cancellationToken);
        if (directoryCreation.IsOperationSuccessful)
        {
          result.TableStorage = await _tableServiceClient.CreateOrGetTableAsync(GetTableStorageName(fileSystemName, fileSystemId.ToString()), cancellationToken);
          result.IsOperationSuccessful = true;

          if (result.TableStorage == null)
          {
            await result.FileSystemClient.DeleteDirectoryIfExistsAsync(fileSystemId.ToString(), cancellationToken);
            result.IsOperationSuccessful = false;
          }
        }
      }

      return result;
    }
    #endregion

    #region Delete File Systems
    public async Task<OperationStatus> DeleteFileSystemAsync(string fileSystemName, string fileSystemId, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException(nameof(fileSystemName)) : string.Empty;

      OperationStatus result = default;

      var fileSystem = _fileSystemServiceClient.GetFileSystem(fileSystemName);
      var table = await _tableServiceClient.GetTableAsync(GetTableStorageName(fileSystemName, fileSystemId.ToString()), cancellationToken);
      if (fileSystem != null && table != null)
      {
        result = await _tableServiceClient.DeleteTableIfExistsAsync(GetTableStorageName(fileSystemName, fileSystemId.ToString()), cancellationToken);
        if (result.IsOperationSuccessful)
        {
          result = await fileSystem.DeleteDirectoryIfExistsAsync(fileSystemId.ToString(), cancellationToken);
        }
      }

      return result;
    }
    #endregion

    public void GetTableEntityAndFileSystemClient(string fileSystemId, string fileSystemName, out ITableStorage tableEntity, out IFileSystemClient fileSystemClient)
    {
      ITableStorage table = default;
      IFileSystemClient client = default;

      var createdFileSystem = CreateOrGetFileSystemAsync(fileSystemName, fileSystemId, CancellationToken.None).Result;
      if (createdFileSystem.IsOperationSuccessful)
      {
        table = createdFileSystem.TableStorage;
        client = createdFileSystem.FileSystemClient;
      }

      tableEntity = table;
      fileSystemClient = client;
    }

    public async Task<ImageUrlDto> GenerateImageUrlAsync(string entityName, string entityId, string fileId, int? expirationTime, CancellationToken cancellationToken, string url = null)
    {
      var result = default(ImageUrlDto);

      GetTableEntityAndFileSystemClient(entityId, entityName, out var tableStorage, out var fileSystemClient);
      if (tableStorage != null && fileSystemClient != null)
      {
        var filePath = url;
        if (string.IsNullOrWhiteSpace(url))
        {
          var tableEntity = await tableStorage.GetEntityAsync<TableFileEntity>(entityId.ToString(), fileId, cancellationToken);
          filePath = tableEntity is null ? string.Empty : $"{entityId}/{tableEntity.FolderPath}/{tableEntity.FileName}";
        }
        var newImageUrl = string.IsNullOrWhiteSpace(filePath) ? default : fileSystemClient.GenerateImageUrl(filePath, expirationTime);
        if (newImageUrl != null)
        {
          result = newImageUrl;
        }
      }

      return result;
    }


    #region Helpers
    private string GetTableStorageName(string iTableName, string iTableId)
    {
      return string.Concat(iTableName, iTableId);
    }
    #endregion
  }
}
