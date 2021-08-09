using AutoMapper;
using Fixit.FileManagement.Lib.Extensions;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fixit.Core.DataContracts.Events.EventGrid.Managers;
using Fixit.Core.Storage.DataContracts.FileSystem.Directories;
using Fixit.Core.Storage.DataContracts.FileSystem.EventDefinitions;
using Fixit.Core.Storage.DataContracts.FileSystem.Events;
using Fixit.Core.Storage.DataContracts.FileSystem.Files;
using Fixit.Core.Storage.DataContracts.FileSystem.Models;
using Fixit.Core.Storage.FileSystem.Adapters;
using Fixit.Core.Storage.FileSystem.Constants;
using Fixit.Core.Storage.FileSystem.Managers.Internal;
using Fixit.Core.Storage.Storage.Blob.Adapters;

namespace Fixit.FileManagement.Lib.Adapters
{
  public class FileSystemClientAdapter : DataLakeFileSystemManager
  {
    private readonly IMapper _mapper;
    private IEventGridTopicServiceClient _regenerateImageUrlTopicServiceClient;

    private readonly string _thumbnailContainerName;

    public FileSystemClientAdapter(IDataLakeFileSystemAdapter dataLakeFileSystemClient,
                                   IBlobStorageClientAdapter cloudBlobContainer,
                                   IMapper mapper,
                                   IConfiguration configuration,  
                                   EventGridTopicServiceClientResolver eventGridTopicServiceClientResolver) : base(dataLakeFileSystemClient, cloudBlobContainer, mapper)
    {
      _ = configuration ?? throw new ArgumentNullException($"{nameof(FileSystemClientAdapter)} expects a value for {nameof(configuration)}... null argument was provided");
      _mapper = mapper ?? throw new ArgumentNullException($"{nameof(FileSystemClientAdapter)} expects a value for {nameof(mapper)}... null argument was provided");
      _regenerateImageUrlTopicServiceClient = eventGridTopicServiceClientResolver(FileEventDefinitions.RegenerateImageUrl.ToString()) ?? throw new ArgumentNullException($"{nameof(FileSystemClientAdapter)} expects an argument for {nameof(eventGridTopicServiceClientResolver)}. Null argumnent was provided.");

      _thumbnailContainerName = configuration["FIXIT-FMS-THUMBNAILS-CONTAINER"];

      _ = string.IsNullOrWhiteSpace(_thumbnailContainerName) ? throw new ArgumentNullException($"{nameof(FileSystemClientAdapter)} expects the {nameof(configuration)} to have defined the thumbnail container Name as {{FIXIT-FMS-THUMBNAILS-CONTAINER}} ") : string.Empty;
    }

    public override FileSystemDirectoryDto GetDirectoryItems(string prefix)
    {
      var result = base.GetDirectoryItems(prefix);
      var files = result.DirectoryItems.Select(file => _mapper.Map<FileSystemFileDto, FileToRegenerateUrlDto>(file));
      PublishRegenerateImageUrlEvents(nameof(GetDirectoryItems), files);

      return result;
    }

    public override async Task<FileSystemDirectoryDto> GetDirectoryItemsAsync(string prefix, CancellationToken cancellationToken)
    {
      var result = await base.GetDirectoryItemsAsync(prefix, cancellationToken);
      var files = result.DirectoryItems.Select(file => _mapper.Map<FileSystemFileDto, FileToRegenerateUrlDto>(file));
      PublishRegenerateImageUrlEvents(nameof(GetDirectoryItemsAsync), files);

      return result;
    }

    public override FileSystemDirectoryDto GetDirectoryStructure(string prefix, bool includeItems = false, bool getSingleLevel = false)
    {
      var result = base.GetDirectoryStructure(prefix, includeItems, getSingleLevel);
      var files = result.ObtainFilesFromDirectory().Select(file => _mapper.Map<FileSystemFileDto, FileToRegenerateUrlDto>(file));
      PublishRegenerateImageUrlEvents(nameof(GetDirectoryStructure), files);

      return result;
    }

    public override async Task<FileSystemDirectoryDto> GetDirectoryStructureAsync(string prefix, CancellationToken cancellationToken, bool includeItems = false, bool getSingleLevel = false)
    {
      var result = await base.GetDirectoryStructureAsync(prefix, cancellationToken, includeItems, getSingleLevel);
      var files = result.ObtainFilesFromDirectory().Select(file => _mapper.Map<FileSystemFileDto, FileToRegenerateUrlDto>(file));
      PublishRegenerateImageUrlEvents(nameof(GetDirectoryStructureAsync), files);

      return result;
    }

    public override FileMetadata GetFileMetadata(string filePath)
    {
      var result = base.GetFileMetadata(filePath);
      var files = new List<FileToRegenerateUrlDto>
      {
          _mapper.Map<FileMetadata, FileToRegenerateUrlDto>(result)
      };

      PublishRegenerateImageUrlEvents(nameof(GetFileMetadata), files);
      return result;
    }

    public override async Task<FileMetadata> GetFileMetadataAsync(string filePath, CancellationToken cancellationToken)
    {
      var result = await base.GetFileMetadataAsync(filePath, cancellationToken);
      var files = new List<FileToRegenerateUrlDto>
      {
          _mapper.Map<FileMetadata, FileToRegenerateUrlDto>(result)
      };

      PublishRegenerateImageUrlEvents(nameof(GetFileMetadataAsync), files);
      return result;
    }

    #region Helpers

    private void PublishRegenerateImageUrlEvents(string subject, IEnumerable<FileToRegenerateUrlDto> fileToRegenerateUrlDtos)
    {
      if (fileToRegenerateUrlDtos != null && fileToRegenerateUrlDtos.Any())
      {

        fileToRegenerateUrlDtos.Select((value, index) => new { Index = index, Value = value })
                               .GroupBy(x => x.Index / FileSystemConstants.MaxFilesToSendToEventGridTrigger)
                               .Select(g => g.Select(x => x.Value).ToList())
                               .ToList()
                               .ForEach(files =>
                               {
                                 var fileRegenerateImageUrlEvent = new EventGridEvent()
                                 {
                                   EventTime = DateTime.UtcNow,
                                   DataVersion = FmsAssemblyInfo.DataVersion,
                                   Subject = subject,
                                   EventType = subject,
                                   Id = Guid.NewGuid().ToString(),
                                   Data = new RegenerateImageUrlEvent { FilesToRegenerateUrls = files }
                                 };

                                 _regenerateImageUrlTopicServiceClient.PublishEventsToTopicAsync(new List<EventGridEvent> { fileRegenerateImageUrlEvent }, CancellationToken.None);
                               });
      }
    }

    #endregion
  }
}
