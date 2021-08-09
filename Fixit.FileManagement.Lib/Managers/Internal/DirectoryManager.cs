using AutoMapper;
using Fixit.Core.DataContracts;
using Microsoft.Azure.EventGrid.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Fixit.Core.DataContracts.Events.EventGrid.Managers;
using Fixit.Core.Storage.DataContracts.FileSystem.Comparers;
using Fixit.Core.Storage.DataContracts.FileSystem.Directories;
using Fixit.Core.Storage.DataContracts.FileSystem.Directories.Requests;
using Fixit.Core.Storage.DataContracts.FileSystem.EventDefinitions;
using Fixit.Core.Storage.DataContracts.FileSystem.Events;
using Fixit.Core.Storage.DataContracts.FileSystem.Files;
using Fixit.Core.Storage.FileSystem.Constants;

[assembly: InternalsVisibleTo("Fixit.FileeManagement.WebApi.UnitTests")]
namespace Fixit.FileManagement.Lib.Managers.Internal
{

  internal class DirectoryManager : IDirectoryManager
  {
    private readonly IFileSystemManager _fileSystemManager;
    private readonly IMapper _mapper;
    private readonly IEventGridTopicServiceClient _onRegenerateImageUrlTopicServiceClient;

    public DirectoryManager(IFileSystemManager fileSystemManager,
                            IMapper mapper,
                            EventGridTopicServiceClientResolver eventGridTopicServiceClientResolver)
    {
      _mapper = mapper ?? throw new ArgumentNullException($"{nameof(DirectoryManager)} expects a value for {nameof(mapper)}... null argument was provided");
      _fileSystemManager = fileSystemManager ?? throw new ArgumentNullException($"{nameof(DirectoryManager)} expects a value for {nameof(fileSystemManager)}... null argument was provided");
      _onRegenerateImageUrlTopicServiceClient = eventGridTopicServiceClientResolver(FileEventDefinitions.RegenerateImageUrl.ToString()) ?? throw new ArgumentNullException($"{nameof(FileManager)} expects an argument for {nameof(eventGridTopicServiceClientResolver)}. Null argumnent was provided.");
    }

    #region Creating Directories

    public async Task<OperationStatus> CreateDirectoryAsync(string fileSystemName, long fileSystemId, DirectoryCreateRequestDto directoryCreateRequestDto, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = directoryCreateRequestDto ?? throw new ArgumentNullException($"expects an argument for {nameof(directoryCreateRequestDto)}. Null argumnent was provided.");

      var result = default(OperationStatus);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var _, out var fileSystem);
      if (fileSystem != null && await fileSystem.IsDirectoryCreatedAsync(fileSystemId.ToString(), cancellationToken))
      {
        var folderPath = $"{fileSystemId.ToString()}/{directoryCreateRequestDto.DirectoryPathToCreate}";

        result = await fileSystem.CreateDirectoryIfNotExistsAsync(folderPath, cancellationToken);
      }
      return result;
    }

    #endregion

    #region Deleting Directories

    public async Task<OperationStatus> DeleteDirectoryAsync(string fileSystemName, long fileSystemId, string folderPath, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = string.IsNullOrWhiteSpace(folderPath) ? throw new ArgumentNullException($"expects the {nameof(folderPath)} to be defined") : string.Empty;

      var result = default(OperationStatus);
      var fullFilePath = $"{fileSystemId.ToString()}/{folderPath}";

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var table, out var fileSystem);
      if (fileSystem != null && await fileSystem.IsDirectoryCreatedAsync(fullFilePath, cancellationToken) && table != null)
      {
        result = await fileSystem.DeleteDirectoryIfExistsAsync(fullFilePath, cancellationToken);

        if (result.IsOperationSuccessful)
        {
          var directoryEntities = await table.GetEntitiesLikePathAsync(fileSystemId.ToString(), folderPath, cancellationToken);
          using var enumerator = directoryEntities.GetEnumerator();
          while (enumerator.MoveNext())
          {
            var tableFileEntity = enumerator.Current;

            if (tableFileEntity != null)
            {
              await table.DeleteEntityIfExistsAsync(tableFileEntity, cancellationToken);
            }
          }
          enumerator.Dispose();
        }
      }

      return result;
    }

    #endregion

    #region Renaming Directories

    public async Task<OperationStatus> RenameDirectoryAsync(string fileSystemName, long fileSystemId, DirectoryRenameRequestDto directoryRenameRequestDto, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;
      _ = directoryRenameRequestDto ?? throw new ArgumentNullException($"expects an argument for {nameof(directoryRenameRequestDto)}. Null argumnent was provided.");

      var result = default(OperationStatus);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out var table, out var fileSystem);
      var currentDir = $"{fileSystemId.ToString()}/{directoryRenameRequestDto.CurrentDirectoryPath}";
      var renamedDir = $"{fileSystemId.ToString()}/{directoryRenameRequestDto.RenamedDirectoryPath}";

      if (fileSystem != null && await fileSystem.IsDirectoryCreatedAsync(currentDir, cancellationToken) && table != null)
      {
        result = await fileSystem.RenameDirectoryAsync(currentDir, renamedDir, cancellationToken);

        if (result.IsOperationSuccessful)
        {
          var currentEntities = await table.GetEntitiesLikePathAsync(fileSystemId.ToString(), directoryRenameRequestDto.CurrentDirectoryPath, cancellationToken);

          using var enumerator = currentEntities.GetEnumerator();

          while (enumerator.MoveNext())
          {
            var tableFileEntity = enumerator.Current;

            if (tableFileEntity != null)
            {
              tableFileEntity.FolderPath = tableFileEntity.FolderPath.Replace(directoryRenameRequestDto.CurrentDirectoryPath.Trim('/'), directoryRenameRequestDto.RenamedDirectoryPath.Trim('/'));
              await table.InsertOrReplaceEntityAsync(tableFileEntity, cancellationToken);
            }
          }
          enumerator.Dispose();

          if (currentEntities != null && currentEntities.Any())
          {

            currentEntities.Select(f => new FileToRegenerateUrlDto { FileId = Guid.Parse(f.RowKey), EntityId = fileSystemId, EntityName = fileSystemName })
                           .Select((value, index) => new { Index = index, Value = value })
                           .GroupBy(x => x.Index / FileSystemConstants.MaxFilesToSendToEventGridTrigger)
                           .Select(g => g.Select(x => x.Value).ToList())
                           .ToList()
                           .ForEach(files =>
                           {
                             var fileRegenerateImageUrlEvent = new EventGridEvent()
                             {
                               EventTime = DateTime.UtcNow,
                               DataVersion = FmsAssemblyInfo.DataVersion,
                               Subject = nameof(RenameDirectoryAsync),
                               EventType = nameof(RenameDirectoryAsync),
                               Id = Guid.NewGuid().ToString(),
                               Data = new RegenerateImageUrlEvent { FilesToRegenerateUrls = files }
                             };

                             _onRegenerateImageUrlTopicServiceClient.PublishEventsToTopicAsync(new List<EventGridEvent> { fileRegenerateImageUrlEvent }, cancellationToken);
                           });
          }
        }
      }

      return result;
    }

    #endregion

    #region Get Directory Structure

    public async Task<FileSystemRootDirectoryDto> GetDirectoryStructureAsync(string fileSystemName, long fileSystemId, string folderPath, CancellationToken cancellationToken, bool includeItems = false, bool singleLevel = false)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;

      var fileSystemItemVm = default(FileSystemRootDirectoryDto);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out _, out var fileSystem);
      if (fileSystem != null)
      {
        fileSystemItemVm = new FileSystemRootDirectoryDto();

        string path = string.IsNullOrWhiteSpace(folderPath) || folderPath == "/" ? $"{fileSystemId}/" : $"{fileSystemId}/{folderPath}";
        fileSystemItemVm.DirectoryInfo = await fileSystem.GetDirectoryStructureAsync(singleLevel ? path : $"{fileSystemId}/", cancellationToken, includeItems, singleLevel);

        if (fileSystemItemVm.DirectoryInfo != null)
        {
          List<FileTagDto> fileTags = new List<FileTagDto>();
          GetAllTagsFromDirectories(fileSystemItemVm.DirectoryInfo, ref fileTags);
          fileSystemItemVm.DirectoryTags = fileTags.Distinct(new FileTagDtoComparer()).ToList();
        }
      }

      return fileSystemItemVm;
    }

    public FileSystemRootDirectoryDto GetDirectoryStructure(string fileSystemName, long fileSystemId, string folderPath, bool includeItems = false, bool singleLevel = false)
    {
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;

      var fileSystemItemVm = default(FileSystemRootDirectoryDto);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out _, out var fileSystem);
      if (fileSystem != null)
      {
        fileSystemItemVm = new FileSystemRootDirectoryDto();

        string path = string.IsNullOrWhiteSpace(folderPath) || folderPath == "/" ? $"{fileSystemId}/" : $"{fileSystemId}/{folderPath}";
        fileSystemItemVm.DirectoryInfo = fileSystem.GetDirectoryStructure(singleLevel ? path : fileSystemId.ToString(), includeItems, singleLevel);

        if (fileSystemItemVm.DirectoryInfo != null)
        {
          List<FileTagDto> fileTags = new List<FileTagDto>();
          GetAllTagsFromDirectories(fileSystemItemVm.DirectoryInfo, ref fileTags);
          fileSystemItemVm.DirectoryTags = fileTags.Distinct(new FileTagDtoComparer()).ToList();
        }
      }

      return fileSystemItemVm;
    }


    public async Task<FileSystemDirectoryItemsDto> GetDirectoryItemsAsync(string fileSystemName, long fileSystemId, string folderPath, CancellationToken cancellationToken, bool ignorePrefix = false, bool includeItems = true)
    {
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;

      var fileSystemItemDto = default(FileSystemDirectoryDto);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out _, out var fileSystem);
      if (fileSystem != null)
      {
        string path = folderPath == "/" ? $"{fileSystemId}/" : $"{fileSystemId}/{folderPath}";

        fileSystemItemDto = await fileSystem.GetDirectoryItemsAsync(path, cancellationToken);
      }

      return _mapper.Map<FileSystemDirectoryDto, FileSystemDirectoryItemsDto>(fileSystemItemDto);
    }

    public FileSystemDirectoryItemsDto GetDirectoryItems(string fileSystemName, long fileSystemId, string folderPath, bool ignorePrefix = false, bool includeItems = true)
    {
      _ = string.IsNullOrWhiteSpace(fileSystemName) ? throw new ArgumentNullException($"expects the {nameof(fileSystemName)} to be defined") : string.Empty;

      var fileSystemItemDto = default(FileSystemDirectoryDto);

      _fileSystemManager.GetTableEntityAndFileSystemClient(fileSystemId, fileSystemName, out _, out var fileSystem);
      if (fileSystem != null)
      {
        string path = folderPath == "/" ? $"{fileSystemId}/" : $"{fileSystemId}/{folderPath}";

        fileSystemItemDto = fileSystem.GetDirectoryItems(path);
      }

      return _mapper.Map<FileSystemDirectoryDto, FileSystemDirectoryItemsDto>(fileSystemItemDto);
    }

    #endregion

    #region Helpers   
    private void GetAllTagsFromDirectories(FileSystemDirectoryDto rootDirectoryNode, ref List<FileTagDto> fileTags)
    {
      var fileTagsForFolder = rootDirectoryNode.DirectoryItems.Select(item => item.FileTags);
      if (fileTagsForFolder != null && fileTagsForFolder.Any())
      {
        foreach (var tags in fileTagsForFolder)
        {
          if (tags != null)
          {
            fileTags.AddRange(tags);
          }
        }
      }

      foreach (var item in rootDirectoryNode.Directories)
      {
        GetAllTagsFromDirectories(item, ref fileTags);
      }
    }
    #endregion
  }
}
