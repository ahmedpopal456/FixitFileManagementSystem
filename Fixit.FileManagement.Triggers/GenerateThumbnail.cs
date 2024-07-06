using ImageMagick;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fixit.Core.Storage.DataContracts.FileSystem.Files;
using Fixit.Core.Storage.FileSystem;
using Fixit.Core.Storage.FileSystem.Managers;

namespace Fixit.FileManagement.Triggers
{
  public class GenerateThumbnail
  {
    private readonly string _thumbnailContainerName;
    private readonly string _assetContainerName;
    private readonly int _thumbnailLinkExpiryTime;

    private readonly IFileSystemClient _thumbnailFileSystemClient;
    private readonly IFileSystemClient _insightsFileSystemClient;
    private readonly IFileSystemClient _assetFileSystemClient;

    private readonly IConfiguration _configuration;

    public GenerateThumbnail(IFileSystemFactory fileSystemFactory,
                             IConfiguration configuration)
    {
      if (fileSystemFactory == null)
      {
        throw new ArgumentNullException($"{nameof(GenerateThumbnail)} expects a value for {nameof(fileSystemFactory)}... null argument was provided");
      }

      _configuration = configuration ?? throw new ArgumentNullException($"{nameof(GenerateThumbnail)} expects a value for {nameof(configuration)}... null argument was provided");

      _thumbnailContainerName = configuration["FIXIT-FMS-THUMBNAILS-CONTAINER"];
      _assetContainerName = configuration["FIXIT-FMS-ASSETS-CONTAINER"];
      _thumbnailLinkExpiryTime = int.Parse(configuration["FIXIT-FMS-THUMBNAILLINK-EXPIRYTIME"]);

      if (string.IsNullOrWhiteSpace(_thumbnailContainerName))
      {
        throw new ArgumentNullException($"{nameof(GenerateThumbnail)} expects the {nameof(configuration)} to have defined the thumbnail container Name as {{FIXIT-FMS-THUMBNAILS-CONTAINER}} ");
      }

      if (string.IsNullOrWhiteSpace(_assetContainerName))
      {
        throw new ArgumentNullException($"{nameof(GenerateThumbnail)} expects the {nameof(configuration)} to have defined the asset container Name as {{FIXIT-FMS-ASSETS-CONTAINER}} ");
      }

      var fileSystemServiceClient = fileSystemFactory.CreateDataLakeFileSystemServiceClient();

      _thumbnailFileSystemClient = fileSystemServiceClient.CreateOrGetFileSystem(_thumbnailContainerName);
      _assetFileSystemClient = fileSystemServiceClient.CreateOrGetFileSystem(_assetContainerName);
    }

    [StorageAccount("FIXIT-FMS-SA-CS")]
    [FunctionName("GenerateThumbnail")]
    public async Task RunAsync([EventGridTrigger] EventGridEvent eventGridEvent, [Blob("{data.url}", FileAccess.Read)] Stream stream, ILogger logger)
    {
      var cancellationToken = new CancellationToken();

      #region INIT

      bool isInvalidFile = false;

      var createdEvent = ((JObject)eventGridEvent.Data).ToObject<StorageBlobCreatedEventData>();

      var fileName = Path.GetFileNameWithoutExtension(createdEvent.Url);
      var fileNameItems = fileName?.Split('_');

      if (fileNameItems != null && fileNameItems.Any())
      {
        isInvalidFile = (fileNameItems.Any(fileNameItem => Guid.TryParse(fileNameItem, out Guid result)) &&
                        fileNameItems.Any(fileNameItem => fileNameItem == _thumbnailContainerName)) ||
                        string.IsNullOrWhiteSpace(Path.GetFileName(fileName));
      }

      #endregion

      #region IMAGE

      if (!isInvalidFile)
      {
        InferFileSystemInfo(createdEvent, out string thumbnailFilePath, out string fileSystemFilePath, out IFileSystemClient fileSystem);
        if (!string.IsNullOrWhiteSpace(thumbnailFilePath) && !string.IsNullOrWhiteSpace(fileSystemFilePath) && fileSystem != null)
        {
          int width = int.Parse(_configuration["ThumbnailWidth"]);
          int height = int.Parse(_configuration["ThumbnailHeight"]);

          // get stream from blob
          Stream blobStream = stream;

          // ready stream for reading
          blobStream.Seek(0, SeekOrigin.Begin);
                   
          // check if file has proper image extension
          if (IsRecognisedImageFile(thumbnailFilePath))
          {
            try
            {
              // try reading image file
              using (var image = new MagickImage(blobStream))
              {
                // resize image and save it in a storage account; then update current file metadata with new image url 
                image.Thumbnail(new MagickGeometry() { IgnoreAspectRatio = false, FillArea = true, Height = height, Width = width });
                var thumbnail = new MemoryStream(image.ToByteArray());

                var uploadResponse = await _thumbnailFileSystemClient.CreateAndUploadFileAsync(thumbnail, thumbnailFilePath, cancellationToken);

                if (uploadResponse.IsOperationSuccessful)
                {
                  var fileInfo = _thumbnailFileSystemClient.GenerateImageUrl(thumbnailFilePath, _thumbnailLinkExpiryTime);

                  var metadata = await fileSystem.GetFileMetadataAsync(fileSystemFilePath, cancellationToken);
                  if (fileInfo != null && !string.IsNullOrWhiteSpace(fileInfo.Url))
                  {
                    metadata.ThumbnailUrl = fileInfo.Url;
                    var setStatus = await fileSystem.SetFileMetadataAsync(fileSystemFilePath,
                                                                          new FileMetadataDto()
                                                                          {
                                                                            ContentType = metadata.ContentType,
                                                                            EntityName = metadata.EntityName,
                                                                            EntityId = metadata.EntityId,
                                                                            ThumbnailUrl = fileInfo.Url,
                                                                            LastUpdatedByUserId = metadata.LastUpdateByUserId,
                                                                            MetadataExtension = metadata.MetadataExtension,
                                                                            MnemonicId = metadata.MnemonicId,
                                                                            MnemonicName = metadata.MnemonicName,
                                                                            Tags = metadata.Tags
                                                                          },
                                                                          cancellationToken);
                  }
                }
              }
            }
            // in case file was named right, but wasn't actually an image
            catch (MagickException exception)
            {
              logger.LogError("An error occurred during the processing of images", exception);
            }
          }
        }
      }

      #endregion
    }

    #region Helper Methods

    private bool IsRecognisedImageFile(string fileName)
    {
      var result = false;

      string targetExtension = Path.GetExtension(fileName);
      if (!String.IsNullOrEmpty(targetExtension))
      {
        targetExtension = $"*{targetExtension.ToLowerInvariant()}";
      }

      if (!string.IsNullOrWhiteSpace(targetExtension))
      {
        foreach (System.Drawing.Imaging.ImageCodecInfo imageCodec in System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders())
        {
          var extensions = imageCodec.FilenameExtension.ToLower().Split(';');

          if (extensions.Contains(targetExtension.ToLower()))
          {
            result = true;
            break;
          }
        }
      }
      return result;
    }

    public void InferFileSystemInfo(StorageBlobCreatedEventData storageBlobCreatedEventData, out string thumbnailFilePath, out string fileSystemFilePath, out IFileSystemClient fileSystem)
    {
      thumbnailFilePath = default;
      fileSystemFilePath = default;

      fileSystem = default;

      var index = default(int);
      var name = default(string);

      var assetsIndex = storageBlobCreatedEventData.Url.IndexOf(_assetContainerName);

      if (assetsIndex > 0)
      {
        fileSystem = _assetFileSystemClient;
        name = _assetContainerName;
        index = assetsIndex;
      }

      if (index != default && !string.IsNullOrWhiteSpace(name))
      {
        fileSystemFilePath = storageBlobCreatedEventData.Url.Substring(index)
                                                            .Replace(name, "")
                                                            .Trim('/');

        var extension = Path.GetExtension(fileSystemFilePath);
        thumbnailFilePath = $"{name}/{Path.Combine(Path.GetDirectoryName(fileSystemFilePath), Path.GetFileNameWithoutExtension(fileSystemFilePath))}_{Guid.NewGuid()}_{_thumbnailContainerName}.{extension.TrimStart('.')}";
      }
    }

    #endregion
  }
}
