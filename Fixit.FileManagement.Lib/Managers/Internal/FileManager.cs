using AutoMapper;
using Fixit.Core.DataContracts;
using Fixit.FileManagement.Lib.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Fixit.Core.DataContracts.Events.EventGrid.Managers;
using Fixit.Core.Storage.DataContracts.FileSystem.EventDefinitions;
using Fixit.Core.Storage.DataContracts.FileSystem.Events;
using Fixit.Core.Storage.DataContracts.FileSystem.Files;
using Fixit.Core.Storage.DataContracts.FileSystem.Files.Requests;
using Fixit.Core.Storage.DataContracts.FileSystem.Files.Responses;
using Fixit.Core.Storage.DataContracts.FileSystem.Models;
using Fixit.Core.Storage.DataContracts.Helpers;
using Fixit.Core.Storage.DataContracts.TableEntities;
using Fixit.Core.Storage.FileSystem.Managers;
using Fixit.Core.Storage.Storage;
using Fixit.Core.Storage.Storage.Table.Managers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventGrid.Models;

[assembly: InternalsVisibleTo("Fixit.FileeManagement.WebApi.UnitTests")]
namespace Fixit.FileManagement.Lib.Managers.Internal
{
  internal class FileManager : IFileManager
  {
    private readonly IFileSystemManager _fileSystemManager;
    private readonly ITableStorage _configurationTable;
    private readonly IMapper _mapper;
    private readonly IEventGridTopicServiceClient _onImageUrlsUpdateTopicServiceClient;
    private readonly IEventGridTopicServiceClient _onRegenerateImageUrlTopicServiceClient;

    private readonly string _fileSystemConfigurationTableName;
    private readonly string _fileSystemConfigurationSettings;
    private readonly string _thumbnailContainerName;
    private readonly int _downloadLinkExpiryTime;
    private readonly int _thumbnailLinkExpiryTime;
    private readonly int _defaultLinkExpiryTime;
    private readonly string _defaultFileCount = Convert.ToString(1);

    public FileManager(IConfiguration configuration,
                       IFileSystemManager fileSystemManager,
                       IStorageFactory storageFactory,
                       IMapper mapper,
                       EventGridTopicServiceClientResolver eventGridTopicServiceClientResolver)
    {
   

      _ = storageFactory ?? throw new ArgumentNullException($"{nameof(FileManager)} expects a value for {nameof(storageFactory)}... null argument was provided");
      _ = configuration ?? throw new ArgumentNullException($"{nameof(FileManager)} expects a value for {nameof(configuration)}... null argument was provided");
      _mapper = mapper ?? throw new ArgumentNullException($"{nameof(FileManager)} expects a value for {nameof(mapper)}... null argument was provided");
      _fileSystemManager = fileSystemManager ?? throw new ArgumentNullException($"{nameof(FileManager)} expects a value for {nameof(fileSystemManager)}... null argument was provided");

      _fileSystemConfigurationTableName = configuration["FIXIT-FMS-CONFIGURATION-TABLE"];
      _fileSystemConfigurationSettings = configuration["FIXIT-FMS-CONFIGURATION-SETTINGS"];
      _thumbnailContainerName = configuration["FIXIT-FMS-THUMBNAILS-CONTAINER"];
      _downloadLinkExpiryTime = int.Parse(configuration["FIXIT-FMS-DOWNLOADLINK-EXPIRYTIME"]);
      _thumbnailLinkExpiryTime = int.Parse(configuration["FIXIT-FMS-THUMBNAILLINK-EXPIRYTIME"]);
      _defaultLinkExpiryTime = int.Parse(configuration["FIXIT-FMS-BASELINK-EXPIRYTIME"]);

      _ = string.IsNullOrWhiteSpace(_thumbnailContainerName) ? throw new ArgumentNullException($"{nameof(FileManager)} expects the {nameof(configuration)} to have defined the thumbnail container Name as {{FIXIT-FMS-THUMBNAILS-CONTAINER}} ") : string.Empty;
      _ = string.IsNullOrWhiteSpace(_fileSystemConfigurationTableName) ? throw new ArgumentNullException($"{nameof(FileManager)} expects the {nameof(configuration)} to have defined the configuration table Name as {{FIXIT-FMS-CONFIGURATION-TABLE}} ") : string.Empty;
      _ = string.IsNullOrWhiteSpace(_fileSystemConfigurationSettings) ? throw new ArgumentNullException($"{nameof(FileManager)} expects the {nameof(configuration)} to have defined the configuration settings Name as {{FIXIT-FMS-CONFIGURATION-SETTINGS}} ") : string.Empty;

      var tableServiceClient = storageFactory.CreateTableStorageClient();
      _configurationTable = tableServiceClient.CreateOrGetTable(_fileSystemConfigurationTableName);

      if (_configurationTable.GetEntity<TableFileSystemInformationEntity>(_fileSystemConfigurationSettings, _fileSystemConfigurationSettings) == null)
      {
        _configurationTable.InsertOrReplaceEntity(new TableFileSystemInformationEntity() { PartitionKey = _fileSystemConfigurationSettings, RowKey = _fileSystemConfigurationSettings, FileCount = 0 });
      }
      _onImageUrlsUpdateTopicServiceClient = eventGridTopicServiceClientResolver(FileEventDefinitions.ImageUrlsUpdate.ToString()) ?? throw new ArgumentNullException($"{nameof(FileManager)} expects an argument for {nameof(eventGridTopicServiceClientResolver)}. Null argumnent was provided.");
      _onRegenerateImageUrlTopicServiceClient = eventGridTopicServiceClientResolver(FileEventDefinitions.RegenerateImageUrl.ToString()) ?? throw new ArgumentNullException($"{nameof(FileManager)} expects an argument for {nameof(eventGridTopicServiceClientResolver)}. Null argumnent was provided.");
    }

    #region Get File
    public async Task<FileStreamResult> GetFileAsync(string fileSystemName, long fileSystemId, Guid fileId, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = Guid.Empty.Equals(fileId) ? throw new ArgumentNullException($"expects the {nameof(fileId)} to be defined") : Guid.Empty;     

      var result = default(FileDataResponseDto);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var table, out var fileSystemClient);
      if (table != null && fileSystemClient != null)
      {
        var tableEntity = table?.GetEntity<TableFileEntity>(fileSystemId.ToString(), fileId.ToString());

        if (tableEntity != null)
        {
          var filePath = $"{fileSystemId.ToString()}/{tableEntity.FolderPath}/{tableEntity.FileName}";

          var fileResponse = await fileSystemClient.GetFileAsync(filePath, cancellationToken);

          if (fileResponse != null && fileResponse.Length > 0)
          {
            result = new FileDataResponseDto()
            {
              FileStream = fileResponse,
              FileId = fileId,
              IsOperationSuccessful = true
            };
          }
        }
      }

      return new FileStreamResult(result.FileStream, "application/octet-stream");
    }
    #endregion

    #region Get File Info
    public async Task<FileResponseDto> GetFileInfoAsync(string fileSystemName, long fileSystemId, Guid fileId, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = Guid.Empty.Equals(fileId) ? throw new ArgumentNullException($"expects the {nameof(fileId)} to be defined") : Guid.Empty;

      var result = default(FileResponseDto);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var table, out var fileSystemClient);
      if (table != null && fileSystemClient != null)
      {
        var tableEntity = await table.GetEntityAsync<TableFileEntity>(fileSystemId.ToString(), fileId.ToString(), cancellationToken);

        if (tableEntity != null)
        {
          var resultItem = await GetFileResponseAsync(fileSystemId, tableEntity, fileSystemClient, cancellationToken);
          if (resultItem.IsOperationSuccessful)
          {
            if (resultItem?.FileInfo.ImageUrl != null)
            {
              resultItem.FileInfo.ImageUrl.Url = HttpUtility.UrlDecode(resultItem.FileInfo.ImageUrl.Url);
            }           
            result = resultItem;
          }
        }
      }

      return result;
    }

    public async Task<IEnumerable<FileResponseDto>> GetFilesInfoAsync(string fileSystemName, long fileSystemId, IEnumerable<Guid> fileIds, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = fileIds.Any(item => Guid.Empty.Equals(item)) ? throw new ArgumentNullException($"expects the all {nameof(fileIds)} to be defined") : Guid.Empty;

      var results = default(List<FileResponseDto>);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var table, out var fileSystemClient);
      if (table != null && fileSystemClient != null)
      {
        results = new List<FileResponseDto>();

        using var enumerator = fileIds.GetEnumerator();

        while (enumerator.MoveNext())
        {
          var fileId = enumerator.Current;
          var tableEntity = await table.GetEntityAsync<TableFileEntity>(fileSystemId.ToString(), fileId.ToString(), cancellationToken);

          if (tableEntity != null)
          {
            var resultItem = await GetFileResponseAsync(fileSystemId, tableEntity, fileSystemClient, cancellationToken);
            if (resultItem?.FileInfo.ImageUrl != null)
            {
              resultItem.FileInfo.ImageUrl.Url = HttpUtility.UrlDecode(resultItem.FileInfo.ImageUrl.Url);
            }
            results.Add(resultItem);
          }
        }
      }

      return results;
    }
    #endregion

    #region Download Files
    public async Task<FileDownloadResponseDto> DownloadFileAsync(string fileSystemName, long fileSystemId, Guid fileId, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = Guid.Empty.Equals(fileId) ? throw new ArgumentNullException($"expects the {nameof(fileId)} to be defined") : Guid.Empty;

      var result = default(FileDownloadResponseDto);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var table, out var fileSystemClient);
      if (table != null && fileSystemClient != null)
      {
        var tableEntity = await table.GetEntityAsync<TableFileEntity>(fileSystemId.ToString(), fileId.ToString(), cancellationToken);

        if (tableEntity != null)
        {
          var filePath = $"{fileSystemId.ToString()}/{tableEntity.FolderPath}/{tableEntity.FileName}";

          var imageUrl = fileSystemClient.GenerateImageUrl(filePath, _downloadLinkExpiryTime);

          if (imageUrl != null)
          {
            result = new FileDownloadResponseDto()
            {
              FileId = fileId,
              DownloadUrl = HttpUtility.UrlDecode(imageUrl.Url),
              FilePath = filePath,
              IsOperationSuccessful = true
            };
          }
        }
      }

      return result;
    }

    public async Task<IEnumerable<FileDownloadResponseDto>> DownloadFilesAsync(string fileSystemName, long fileSystemId, IEnumerable<Guid> fileRequestDtos, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = fileRequestDtos ?? throw new ArgumentNullException($"expects an argument for {nameof(fileRequestDtos)}. Null argumnent was provided.");

      var results = default(IList<FileDownloadResponseDto>);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var table, out var fileSystemClient);
      if (table != null && fileSystemClient != null)
      {
        results = new List<FileDownloadResponseDto>();

        using var enumerator = fileRequestDtos.GetEnumerator();

        while (enumerator.MoveNext())
        {
          var fileRequest = enumerator.Current;

          var tableEntity = await table.GetEntityAsync<TableFileEntity>(fileSystemId.ToString(), fileRequest.ToString(), cancellationToken);
          if (tableEntity != null)
          {
            var filePath = $"{fileSystemId.ToString()}/{tableEntity.FolderPath}/{tableEntity.FileName}";
            var imageUrl = fileSystemClient.GenerateImageUrl(filePath, _downloadLinkExpiryTime);
            var resultItem = new FileDownloadResponseDto()
            {
              IsOperationSuccessful = false
            };

            if (imageUrl != null)
            {
              resultItem.FileId = fileRequest;
              resultItem.DownloadUrl = HttpUtility.UrlDecode(imageUrl.Url);
              resultItem.FilePath = filePath;
              resultItem.IsOperationSuccessful = true;
            }

            results.Add(resultItem);
          }
        }
      }

      return results;
    }

    #endregion

    #region Upload Files

    public async Task<FileUploadResponseDto> UploadFileAsync(string fileSystemName, long fileSystemId, FileUploadRequestDto fileUploadRequestDto, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = fileUploadRequestDto ?? throw new ArgumentNullException($"expects an argument for {nameof(fileUploadRequestDto)}. Null argumnent was provided.");

      var file = fileUploadRequestDto.FormFile;

      var result = default(FileUploadResponseDto);
      var fileContent = new MemoryStream();
      var metadata = _mapper.Map<FileMetadataSummary, FileMetadataDto>(fileUploadRequestDto.FileMetadataSummary);

      // Get System Information 
      var fileSystemInformation = _configurationTable.GetEntity<TableFileSystemInformationEntity>(_fileSystemConfigurationSettings, _fileSystemConfigurationSettings);

      // Set Metadata
      metadata.EntityId = fileSystemId.ToString();
      metadata.EntityName = fileSystemName;
      metadata.MnemonicId = fileSystemInformation != null ? fileSystemInformation.FileCount.ToString() : _defaultFileCount;

      // Get FileSystem and Table
      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var table, out var fileSystemClient);
      if (table != null && fileSystemClient != null)
      {
        // If Entities already existed in the same path, get them so they can be deleted upon successful upload
        var entities = await table.GetEntitiesByPathAsync(fileSystemId.ToString(), fileUploadRequestDto.FilePathToCreate, cancellationToken);
        var filePath = $"{fileSystemId}/{fileUploadRequestDto.FilePathToCreate}";

        // If a file with the same name exists, append a Guid to make it unique
        if (entities.Any())
        {
          var uniqueGuid = Guid.NewGuid();

          filePath = EmpowerPathHelper.AddGuidToFilePath(filePath, uniqueGuid);
          fileUploadRequestDto.FilePathToCreate = EmpowerPathHelper.AddGuidToFilePath(fileUploadRequestDto.FilePathToCreate, uniqueGuid);
        }

        // Prepare File Upload and Execute 
        result = new FileUploadResponseDto();
        fileContent = await CopyToAndPrepareMemoryStreamAsync(file, fileContent, cancellationToken);

        var uploadResponse = await fileSystemClient.CreateAndUploadFileAsync(fileContent, filePath, cancellationToken, metadata);

        // Insert File Information in Table Storage
        if (uploadResponse.IsOperationSuccessful)
        {
          await UpdateFileSystemStatisticsAsync(fileSystemInformation, cancellationToken);

          var tableEntity = _mapper.Map<FileUploadDto, TableFileEntity>(uploadResponse);

          tableEntity.PartitionKey = fileSystemId.ToString();
          tableEntity.FolderPath = StringHelper.ToAzureDirectoryPath(Path.GetDirectoryName(fileUploadRequestDto.FilePathToCreate));
          tableEntity.FileName = Path.GetFileName(fileUploadRequestDto.FilePathToCreate);

          await _configurationTable.InsertOrReplaceEntityAsync<TableFileSystemInformationEntity>(fileSystemInformation, cancellationToken);

          var insertResult = await table.InsertOrReplaceEntityAsync(tableEntity, cancellationToken);
          if (!insertResult.IsOperationSuccessful)
          {
            // If information upload fails, then the file has to be deleted for consistency
            await fileSystemClient.DeleteFileAsync(filePath, cancellationToken);

            result.IsOperationSuccessful = false;
          }

          result = _mapper.Map<FileUploadDto, FileUploadResponseDto>(uploadResponse);
        }
      }
      return result;
    }

    public async Task<IEnumerable<FileUploadResponseDto>> UploadFilesAsync(string fileSystemName, long fileSystemId, MultiFileUploadRequestDto fileUploadsRequestDto, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = fileUploadsRequestDto ?? throw new ArgumentNullException($"expects an argument for {nameof(fileUploadsRequestDto)}. Null argumnent was provided.");

      var results = new List<FileUploadResponseDto>();

      var fileRequests = fileUploadsRequestDto.FilePathMetadataInfoToDictionary();

      foreach (var fileRequest in fileRequests)
      {
        var result = default(FileUploadResponseDto);

        var filePath = fileRequest.Key;
        var wFormFile = fileUploadsRequestDto.FormFileCollection.SingleOrDefault(item => item.FileName == Path.GetFileName(filePath));

        // Get System Information 
        var fileSystemInformation = _configurationTable.GetEntity<TableFileSystemInformationEntity>(_fileSystemConfigurationSettings, _fileSystemConfigurationSettings);

        // Set Metadata
        var metadata = _mapper.Map<FileMetadataSummary, FileMetadataDto>(fileRequest.Value);
        metadata.EntityId = fileSystemId.ToString();
        metadata.EntityName = fileSystemName;
        metadata.MnemonicId = fileSystemInformation != null ? fileSystemInformation.FileCount.ToString() : _defaultFileCount;

        // Prepare Files Upload and Execute 
        _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var table, out var fileSystemClient);
        if (table != null && fileSystemClient != null)
        {
          // If Entities already existed in the same path, get them so they can be deleted upon successful upload
          var entities = await table.GetEntitiesByPathAsync(fileSystemId.ToString(), filePath, cancellationToken);

          result = new FileUploadResponseDto();

          var fileContent = new MemoryStream();
          var uploadPath = $"{fileSystemId}/{filePath}";

          fileContent = await CopyToAndPrepareMemoryStreamAsync(wFormFile, fileContent, cancellationToken);

          // If a file with the same name exists, append a Guid to make it unique
          if (entities.Any())
          {
            var uniqueGuid = Guid.NewGuid();

            uploadPath = EmpowerPathHelper.AddGuidToFilePath(uploadPath, uniqueGuid);
            filePath = EmpowerPathHelper.AddGuidToFilePath(filePath, uniqueGuid);
          }

          var uploadResponse = fileSystemClient.CreateAndUploadFile(fileContent,
                                                                    uploadPath,
                                                                    metadata);
          // Insert File Information in Table Storage
          if (uploadResponse.IsOperationSuccessful)
          {
            await UpdateFileSystemStatisticsAsync(fileSystemInformation, cancellationToken);

            var tableEntity = _mapper.Map<FileUploadDto, TableFileEntity>(uploadResponse);

            tableEntity.PartitionKey = fileSystemId.ToString();
            tableEntity.FolderPath = StringHelper.ToAzureDirectoryPath(Path.GetDirectoryName(filePath));
            tableEntity.FileName = Path.GetFileName(filePath);

            var insertResult = table.InsertOrReplaceEntity(tableEntity);

            if (!insertResult.IsOperationSuccessful)
            {
              // If information upload fails, then the file has to be deleted for consistency
              fileSystemClient.DeleteFile(uploadPath);
              result.IsOperationSuccessful = false;
            }
            result = _mapper.Map<FileUploadDto, FileUploadResponseDto>(uploadResponse);
          }
        }

        // Add upload results to return response
        results.Add(result);
      }

      return results;
    }

    #endregion

    #region Renaming Files

    public async Task<OperationStatus> RenameFileAsync(string fileSystemName, long fileSystemId, Guid fileId, FileRenameRequestDto fileRenameRequestDto, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = fileRenameRequestDto ?? throw new ArgumentNullException($"expects an argument for {nameof(fileRenameRequestDto)}. Null argumnent was provided.");

      // Prepare for and Rename File
      var result = default(OperationStatus);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var table, out var fileSystem);
      if (table != null && fileSystem != null)
      {
        var tableEntity = await table.GetEntityAsync<TableFileEntity>(fileSystemId.ToString(), fileId.ToString(), cancellationToken);

        if (tableEntity != null)
        {
          var currentFilePath = string.IsNullOrWhiteSpace(tableEntity.FolderPath) ? $"{fileSystemId.ToString()}/{tableEntity.FileName}" : $"{fileSystemId.ToString()}/{tableEntity.FolderPath}/{tableEntity.FileName}";

          var updatedFilePath = $"{fileSystemId.ToString()}/{fileRenameRequestDto.RenamedFilePath}";

          result = await fileSystem.RenameFileAsync(currentFilePath, updatedFilePath, cancellationToken);

          // Update Table Storage Information
          if (result.IsOperationSuccessful)
          {
            tableEntity.FolderPath = StringHelper.ToAzureDirectoryPath(Path.GetDirectoryName(fileRenameRequestDto.RenamedFilePath));
            tableEntity.FileName = Path.GetFileName(fileRenameRequestDto.RenamedFilePath);

            var insertResult = await table.InsertOrReplaceEntityAsync(tableEntity, cancellationToken);

            if (!insertResult.IsOperationSuccessful)
            {
              // If Operation fails, then roll-back to previous filename
              await fileSystem.RenameFileAsync(updatedFilePath, currentFilePath, cancellationToken);
              result.IsOperationSuccessful = false;
            }
            else
            {
              var fileToRegenerate = new FileToRegenerateUrlDto { FileId = fileId, EntityId = fileSystemId, EntityName = fileSystemName };
              var fileRegenerateImageUrlEvent = new EventGridEvent()
              {
                EventTime = DateTime.UtcNow,
                DataVersion = FmsAssemblyInfo.DataVersion,
                Subject = nameof(RenameFileAsync),
                EventType = nameof(RenameFileAsync),
                Id = Guid.NewGuid().ToString(),
                Data = new RegenerateImageUrlEvent { FilesToRegenerateUrls = new List<FileToRegenerateUrlDto> { fileToRegenerate } }
              };

              await _onRegenerateImageUrlTopicServiceClient.PublishEventsToTopicAsync(new List<EventGridEvent> { fileRegenerateImageUrlEvent }, cancellationToken);                          
            }
          }
        }
      }

      return result;
    }

    #endregion

    #region Deleting Files

    public async Task<OperationStatus> DeleteFileAsync(string fileSystemName, long fileSystemId, Guid fileId, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = Guid.Empty.Equals(fileId) ? throw new ArgumentNullException($"expects the {nameof(fileId)} to be defined") : Guid.Empty;    

      var result = default(OperationStatus);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var table, out var fileSystemClient);
      if (table != null && fileSystemClient != null)
      {
        var tableEntity = await table.GetEntityAsync<TableFileEntity>(fileSystemId.ToString(), fileId.ToString(), cancellationToken);

        if (tableEntity != null)
        {
          var filePath = string.IsNullOrWhiteSpace(tableEntity.FolderPath) ? $"{fileSystemId.ToString()}/{tableEntity.FileName}" : $"{fileSystemId.ToString()}/{tableEntity.FolderPath}/{tableEntity.FileName}";

          result = await table.DeleteEntityIfExistsAsync(tableEntity, cancellationToken);

          if (result.IsOperationSuccessful)
          {
            result = await fileSystemClient.DeleteFileAsync(filePath, cancellationToken);
          }
        }
      }

      return result;
    }

    #endregion

    #region Update Files

    public async Task<FileMetadataDto> SetFileMetadataAsync(string fileSystemName, long fileSystemId, Guid fileId, FileMetadataSummary fileMetadataSummary, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = Guid.Empty.Equals(fileId) ? throw new ArgumentNullException($"expects the {nameof(fileId)} to be defined") : Guid.Empty;
      _ = fileMetadataSummary ?? throw new ArgumentNullException($"expects an argument for {nameof(fileMetadataSummary)}. Null argumnent was provided.");   

      var result = default(FileMetadataDto);

      // Get FileSystem and Table
      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var table, out var fileSystemClient);
      if (table != null && fileSystemClient != null)
      {
        var tableEntity = await table.GetEntityAsync<TableFileEntity>(fileSystemId.ToString(), fileId.ToString(), cancellationToken);

        if (tableEntity != null)
        {
          var filePath = $"{fileSystemId}/{tableEntity.FolderPath}/{tableEntity.FileName}";

          var fileResponse = await fileSystemClient.GetFileMetadataAsync(filePath, cancellationToken);

          fileResponse.Tags = string.IsNullOrWhiteSpace(fileMetadataSummary.TagNames) ? null : fileMetadataSummary.TagNames.Split(',', System.StringSplitOptions.RemoveEmptyEntries).Distinct().Select(tag => new FileTagDto() { Name = tag.Trim().ToLowerInvariant() }).ToList();
          fileResponse.MetadataExtension = fileMetadataSummary.MetadataExtension;
          fileResponse.ContentType = fileMetadataSummary.ContentType;
          fileResponse.MnemonicName = fileMetadataSummary.MnemonicName;
          fileResponse.ImageUrl = fileMetadataSummary.ImageUrl ?? fileResponse.ImageUrl;
          fileResponse.ThumbnailUrl = string.IsNullOrWhiteSpace(fileMetadataSummary.ThumbnailUrl) ? fileResponse.ThumbnailUrl : fileMetadataSummary.ThumbnailUrl;

          var metadataDto = _mapper.Map<FileMetadata, FileMetadataDto>(fileResponse);

          var operationStatus = await fileSystemClient.SetFileMetadataAsync(filePath, metadataDto, cancellationToken);
          if (operationStatus.IsOperationSuccessful)
          {
            result = metadataDto;
            var fileImageUrlUpdate = new EventGridEvent()
            {
              EventTime = DateTime.UtcNow,
              DataVersion = FmsAssemblyInfo.DataVersion,
              Subject = nameof(SetFileMetadataAsync),
              EventType = nameof(SetFileMetadataAsync),
              Id = Guid.NewGuid().ToString(),
              Data = new ImageUrlsUpdateEvent()
              {
                FileId = fileResponse.FileId,
                ImageUrl = metadataDto.ImageUrl,
                ThumbnailUrl = metadataDto.ThumbnailUrl
              }
            };

            await _onImageUrlsUpdateTopicServiceClient.PublishEventsToTopicAsync(new List<EventGridEvent> { fileImageUrlUpdate }, CancellationToken.None);
          }
        }
      }

      return result;
    }

    #endregion

    #region Regenerate Urls
   
    public async Task<IEnumerable<Task>> RegenerateUrlsAsync(IEnumerable<FileToRegenerateUrlDto> files, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var tasks = new List<Task>();

      foreach (var file in files)
      {
        tasks.Add(Task.Run(() =>
        {
          var metadataSummary = new FileMetadataSummary();

          StringHelper.ObtainFilePathAndSasExpiryDateFromFileUrl(file.ThumbnailUrl, _thumbnailContainerName, out var thumbnailFilePath, out var thumbnailExpiryDate);
          if (!string.IsNullOrWhiteSpace(thumbnailFilePath) && thumbnailExpiryDate != default(DateTime) && thumbnailExpiryDate <= DateTime.UtcNow)
          {
            var newThumbnailUrl = _fileSystemManager.GenerateImageUrlAsync(_thumbnailContainerName, file.EntityId, file.FileId.ToString(), _thumbnailLinkExpiryTime, cancellationToken, thumbnailFilePath).Result;
            if (!string.IsNullOrWhiteSpace(newThumbnailUrl?.Url))
            {
              metadataSummary.ThumbnailUrl = newThumbnailUrl.Url;
            }
          }

          if (file.ImageUrl == null || file.ImageUrl.ExpiryDate <= DateTime.UtcNow)
          {
            var newImageUrl = _fileSystemManager.GenerateImageUrlAsync(file.EntityName, file.EntityId, file.FileId.ToString(), _defaultLinkExpiryTime, cancellationToken).Result;
            if (newImageUrl != null)
            {
              metadataSummary.ImageUrl = newImageUrl;
            }
          }

          if (!string.IsNullOrWhiteSpace(metadataSummary.ThumbnailUrl) || metadataSummary.ImageUrl != null)
          {
            SetFileMetadataAsync(file.EntityName, file.EntityId, file.FileId, metadataSummary, cancellationToken).Wait();
          }
        }));       
      }

      return tasks;
    }  
   
    #endregion

    #region Helpers

    private async Task<FileResponseDto> GetFileResponseAsync(long fileSystemId, TableFileEntity tableEntity, IFileSystemClient fileSystemClient, CancellationToken cancellationToken)
    {
      var filePath = $"{fileSystemId.ToString()}/{tableEntity.FolderPath}/{tableEntity.FileName}";

      var fileResponse = await fileSystemClient.GetFileMetadataAsync(filePath, cancellationToken);

      var resultItem = new FileResponseDto()
      {
        IsOperationSuccessful = false
      };

      if (fileResponse != null)
      {
        resultItem.FileId = fileResponse.FileId;
        resultItem.FileInfo = _mapper.Map<FileMetadata, FileInfoDto>(fileResponse);
        resultItem.IsOperationSuccessful = true;
        resultItem.FileName = tableEntity.FileName;
      }

      return resultItem;
    }

    private async Task UpdateFileSystemStatisticsAsync(TableFileSystemInformationEntity tableFileSystemInformationEntity, CancellationToken cancellationToken)
    {
      tableFileSystemInformationEntity.FileCount += 1;

      await _configurationTable.InsertOrReplaceEntityAsync<TableFileSystemInformationEntity>(tableFileSystemInformationEntity, cancellationToken);
    }

    private async Task<MemoryStream> CopyToAndPrepareMemoryStreamAsync(IFormFile formFile, MemoryStream destinationStream, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var result = default(MemoryStream);

      try
      {
        var tempStream = destinationStream;

        await formFile.CopyToAsync(tempStream, cancellationToken);
        tempStream.Seek(0, SeekOrigin.Begin);

        result = tempStream;
      }
      catch
      {
        // Fall through
      }

      return result;
    }
    #endregion
  }
}
