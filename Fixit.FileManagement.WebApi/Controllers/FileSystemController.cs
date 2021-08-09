using Fixit.FileManagement.Lib.Managers;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Fixit.Core.Storage.DataContracts.FileSystem.Directories.Requests;
using Fixit.Core.Storage.DataContracts.FileSystem.Files;
using Fixit.Core.Storage.DataContracts.FileSystem.Files.Requests;

namespace Fixit.FileManagement.WebApi.Controllers
{
  [ApiController]
  [Route("api/FileSystem")]
  public class FileSystemController : ControllerBase
  {
    private readonly IFileSystemManager _fileSystemManager;
    private readonly IFileManager _fileManager;
    private readonly IDirectoryManager _directoryManager;

    public FileSystemController(IFileSystemManager fileSystemManager,
                                IFileManager fileManager,
                                IDirectoryManager directoryManager)
    {
      _fileSystemManager = fileSystemManager ?? throw new ArgumentNullException($"{nameof(FileSystemController)} expects a value for {nameof(fileSystemManager)}... null argument was provided");
      _fileManager = fileManager ?? throw new ArgumentNullException($"{nameof(FileSystemController)} expects a value for {nameof(fileManager)}... null argument was provided");
      _directoryManager = directoryManager ?? throw new ArgumentNullException($"{nameof(FileSystemController)} expects a value for {nameof(directoryManager)}... null argument was provided");
    }    

    [HttpGet("{name}/{id}")]
    public async Task<IActionResult> GetFileSystemStructureAsync([FromRoute] string name, [FromRoute] long id,[FromHeader] string folderPath, CancellationToken cancellationToken, bool includeItems, bool singleLevel)
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"The name provided was null or consisted of white spaces...");
      }

      var decodedFolderPath = HttpUtility.UrlDecode(folderPath);
      if (!string.IsNullOrWhiteSpace(folderPath) && (!IsValidFolder(decodedFolderPath) || string.IsNullOrWhiteSpace(name)))
      {
        return BadRequest($"Either the name and/or the specified folder to get was invalid...");
      }

      var getFileStructureResponse = await _directoryManager.GetDirectoryStructureAsync(name, id, decodedFolderPath, cancellationToken, includeItems, singleLevel);

      if (getFileStructureResponse == null)
      {
        return NotFound($"The filesystem specified was not found... was it modified ?");
      }

      var serializedResponse = JsonConvert.SerializeObject(getFileStructureResponse, Formatting.Indented);

      return Ok(serializedResponse);
    }

    [HttpPost("{name}/{id}")]
    public async Task<IActionResult> CreateFileSystemAsync([FromRoute] string name, [FromRoute] long id, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"The name provided was null or consisted of white spaces...");
      }

      var createFileSystemResponse = await _fileSystemManager.CreateOrGetFileSystemAsync(name, id, cancellationToken);

      if (createFileSystemResponse == null)
      {
        return NotFound($"The filesystem specified was not found... was it modified ?");
      }

      return Ok(createFileSystemResponse);
    }

    [HttpDelete("{name}/{id}")]
    public async Task<IActionResult> DeleteFileSystemAsync([FromRoute] string name, [FromRoute] long id, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"The name provided was null or consisted of white spaces...");
      }

      var deleteFileSystemResponse = await _fileSystemManager.DeleteFileSystemAsync(name, id, cancellationToken);

      if (deleteFileSystemResponse == null)
      {
        return NotFound($"The filesystem specified was not found... was it modified ?");
      }

      return Ok(deleteFileSystemResponse);
    }

    [HttpGet("{name}/{id}/Files/{fileId}")]
    public async Task<IActionResult> GetFileInfoAsync([FromRoute] string name, [FromRoute] long id, [FromRoute] string fileId, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!TryValidatingGuid(fileId, out Guid parsedFileId) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"Either the name or/and fileId provided were null or consisted of white spaces...");
      }

      var fileResponse = await _fileManager.GetFileInfoAsync(name, id, parsedFileId, cancellationToken);
      if (fileResponse == null)
      {
        return NotFound($"The file id specified as {fileId}, was not found... was the id modified ?");
      }

      return Ok(fileResponse);
    }

    [HttpGet("{name}/{id}/Files/Download")]
    public async Task<IActionResult> DownloadFilesAsync([FromRoute] string name, [FromRoute] long id, [FromHeader] IEnumerable<Guid> fileIds, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!IsValidMultiFileRequest(fileIds) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"Either the name or/and fileId provided were null or consisted of white spaces...");
      }

      var downloadResponses = await _fileManager.DownloadFilesAsync(name, id, fileIds, cancellationToken);
      if (downloadResponses == null)
      {
        return NotFound($"The filesystem specified was not found... was it modified ?");
      }

      return Ok(downloadResponses);
    }

    [HttpGet("{name}/{id}/Files")]
    public async Task<IActionResult> GetFilesInfo([FromRoute] string name, [FromRoute] long id, [FromHeader] IEnumerable<Guid> fileIds, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!IsValidMultiFileRequest(fileIds) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"Either the name or/and fileId provided were null or consisted of white spaces...");
      }

      var fileResponses = await _fileManager.GetFilesInfoAsync(name, id, fileIds, cancellationToken);
      if (fileResponses == null)
      {
        return NotFound($"The filesystem specified was not found... was it modified ?");
      }

      return Ok(fileResponses);
    }

    [HttpGet("{name}/{id}/Files/{fileId}/Download")]
    public async Task<IActionResult> DownloadFileAsync([FromRoute] string name, [FromRoute] long id, [FromRoute] string fileId, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!TryValidatingGuid(fileId, out Guid parsedFileId) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"Either the name or/and fileId provided were null or consisted of white spaces...");
      }

      var downloadResponse = await _fileManager.DownloadFileAsync(name, id, parsedFileId, cancellationToken);
      if (downloadResponse == null)
      {
        return NotFound($"The file id specified as {fileId}, was not found... was the id modified ?");
      }

      return Ok(downloadResponse);
    }

    [HttpPost("{name}/{id}/Files/Upload"), DisableRequestSizeLimit]
    public async Task<IActionResult> UploadFileAsync([FromRoute] string name, [FromRoute] long id, [FromForm] FileUploadRequestDto fileUploadRequestDto, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!IsValidUploadRequest(fileUploadRequestDto) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"Either the name and/or the requested file specified in {nameof(fileUploadRequestDto)} was invalid...");
      }

      var uploadResponse = await _fileManager.UploadFileAsync(name, id, fileUploadRequestDto, cancellationToken);

      if (uploadResponse == null)
      {
        return NotFound($"The filesystem specified was not found... was it modified ?");
      }

      return Ok(uploadResponse);
    }

    [HttpPost("{name}/{id}/Files/Uploads"), DisableRequestSizeLimit]
    public async Task<IActionResult> UploadFilesAsync([FromRoute] string name, [FromRoute] long id, [FromForm] MultiFileUploadRequestDto multiFileUploadsRequestDto, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!IsValidMultiUploadRequest(multiFileUploadsRequestDto) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"Either the name and/or one or more requested files specified in {nameof(multiFileUploadsRequestDto)} were invalid...");
      }

      var uploadResponse = await _fileManager.UploadFilesAsync(name, id, multiFileUploadsRequestDto, cancellationToken);
      if (uploadResponse == null)
      {
        return NotFound($"The filesystem specified was not found... was it modified ?");
      }

      return Ok(uploadResponse);
    }

    [HttpPut("{name}/{id}/Files/{fileId}/Rename")]
    public async Task<IActionResult> RenameFileAsync([FromRoute] string name, [FromRoute] long id, [FromRoute] string fileId, [FromBody] FileRenameRequestDto fileRenameRequestDto, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!TryValidatingGuid(fileId, out Guid fileGuidId) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"Either the name and/or fileId provided were null or consisted of white spaces...");
      }

      if (!IsValidFileRenameRequest(fileRenameRequestDto))
      {
        return BadRequest($"The specified file to rename was invalid...");
      }

      var fileRenameResponse = await _fileManager.RenameFileAsync(name, id, fileGuidId, fileRenameRequestDto, cancellationToken);
      if (fileRenameResponse == null)
      {
        return NotFound($"The file id specified as {fileId}, was not found... was the id modified ?");
      }

      return Ok(fileRenameResponse);
    }

    [HttpPut("{name}/{id}/Files/{fileId}/Metadata")]
    public async Task<IActionResult> UpdateFileMetadaAsync([FromRoute] string name, [FromRoute] long id, [FromRoute] string fileId, [FromBody] FileMetadataSummary fileMetadataSummary, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!TryValidatingGuid(fileId, out Guid fileGuidId) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"Either the name and/or fileId provided were null or consisted of white spaces...");
      }

      if (!IsValidMetadataUpdateRequest(fileMetadataSummary))
      {
        return BadRequest($"The specified file to rename was invalid...");
      }

      var fileMetadataUpdateResponse = await _fileManager.SetFileMetadataAsync(name, id, fileGuidId, fileMetadataSummary, cancellationToken);
      if (fileMetadataUpdateResponse == null)
      {
        return NotFound($"The file id specified as {fileId}, was not found... was the id modified ?");
      }

      return Ok(fileMetadataUpdateResponse);
    }

    [HttpDelete("{name}/{id}/Files/{fileId}/Delete")]
    public async Task<IActionResult> DeleteFileAsync([FromRoute] string name, [FromRoute] long id, [FromRoute] string fileId, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!TryValidatingGuid(fileId, out Guid parsedFileId) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"Either the name or/and fileId provided were null or consisted of white spaces...");
      }

      var wFileDeleteResponse = await _fileManager.DeleteFileAsync(name, id, parsedFileId, cancellationToken);
      if (wFileDeleteResponse == null)
      {
        return NotFound($"The file id specified as {fileId}, was not found... was the id modified ?");
      }

      return Ok(wFileDeleteResponse);
    }

    [HttpPut("{name}/{id}/Folders/Rename")]
    public async Task<IActionResult> RenameDirectoryAsync([FromRoute] string name, [FromRoute] long id, [FromBody] DirectoryRenameRequestDto directoryRenameRequestDto, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!IsValidFolderRenameRequest(directoryRenameRequestDto) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"Either the name and/or the specified folder to rename was invalid...");
      }

      var wDirectoryRenameResponse = await _directoryManager.RenameDirectoryAsync(name, id, directoryRenameRequestDto, cancellationToken);
      if (wDirectoryRenameResponse == null)
      {
        return NotFound($"The folder specified was not found... was it modified ?");
      }

      return Ok(wDirectoryRenameResponse);
    }

    [HttpDelete("{name}/{id}/Folders/Delete")]
    public async Task<IActionResult> DeleteDirectoryAsync([FromRoute] string name, [FromRoute] long id, [FromHeader] string folderPath, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var decodedFolderPath = HttpUtility.UrlDecode(folderPath);
      if (!IsValidFolder(decodedFolderPath) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"Either the name and/or the specified folder to delete was invalid...");
      }

      var directoryDeleteResponse = await _directoryManager.DeleteDirectoryAsync(name, id, decodedFolderPath, cancellationToken);
      if (directoryDeleteResponse == null)
      {
        return NotFound($"The folder specified was not found... was it modified ?");
      }

      return Ok(directoryDeleteResponse);
    }

    [HttpPost("{name}/{id}/Folders/Create")]
    public async Task<IActionResult> CreateFolderAsync([FromRoute] string name, [FromRoute] long id, [FromBody] DirectoryCreateRequestDto directoryCreateRequestDto, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!IsValidFolderCreateRequest(directoryCreateRequestDto) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"The name and/or the specified folder to create was invalid or contains one or many invalid characters (+)...");
      }

      var createFileSystemResponse = await _directoryManager.CreateDirectoryAsync(name, id, directoryCreateRequestDto, cancellationToken);
      if (createFileSystemResponse == null)
      {
        return NotFound($"The filesystem specified was not found... was it modified?");
      }
      return Ok(createFileSystemResponse);
    }
  
    [HttpGet("{name}/{id}/Folders/Get")]
    public async Task<IActionResult> GetDirectoryAsync([FromRoute] string name, [FromRoute] long id, [FromHeader] string folderPath, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var decodedFolderPath = HttpUtility.UrlDecode(folderPath);
      if (!IsValidFolder(decodedFolderPath) || string.IsNullOrWhiteSpace(name))
      {
        return BadRequest($"Either the name and/or the specified folder to get was invalid...");
      }

      var wDirectoryGetResponse = await _directoryManager.GetDirectoryItemsAsync(name, id, decodedFolderPath, cancellationToken);
      if (wDirectoryGetResponse == null)
      {
        return NotFound($"The folder specified was not found... was it modified ?");
      }

      return Ok(wDirectoryGetResponse);
    }

    #region Helper Methods

    private bool TryValidatingGuid(string iId, out Guid resultingGuid)
    {
      bool isValid = Guid.TryParse(iId, out var guidId) && !Guid.Empty.Equals(guidId);
      resultingGuid = guidId;

      return isValid;
    }

    private bool IsValidUploadRequest(FileUploadRequestDto fileUploadRequestDto)
    {
      bool isValid = fileUploadRequestDto != null &&
                      (fileUploadRequestDto.FileMetadataSummary != null &&
                      fileUploadRequestDto.FormFile != null &&
                      !String.IsNullOrWhiteSpace(fileUploadRequestDto.FilePathToCreate));
      return isValid;
    }

    private bool IsValidMultiUploadRequest(MultiFileUploadRequestDto multifileUploadRequestDto)
    {
      bool isValid = multifileUploadRequestDto != null &&
                     multifileUploadRequestDto.FilePathMetadataInfoToDictionary() != null &&
                     multifileUploadRequestDto.FormFileCollection.All(item => item != null && multifileUploadRequestDto.FilePathMetadataInfoToDictionary().Any(filepath => item.FileName == Path.GetFileName(filepath.Key)));

      return isValid;
    }

    private bool IsValidMetadataUpdateRequest(FileMetadataSummary fileMetadataSummary)
    {
      bool isValid = fileMetadataSummary != null;
      return isValid;
    }

    private bool IsValidFileRenameRequest(FileRenameRequestDto fileRenameRequestDto)
    {
      bool isValid = fileRenameRequestDto != null &&
                      !string.IsNullOrWhiteSpace(fileRenameRequestDto.RenamedFilePath) && !string.IsNullOrWhiteSpace(Path.GetFileName(fileRenameRequestDto.RenamedFilePath));

      return isValid;
    }

    private bool IsValidFolder(string folderPath)
    {
      bool isValid = (folderPath == "/" || !string.IsNullOrWhiteSpace(Path.GetDirectoryName(folderPath))) && string.IsNullOrWhiteSpace(Path.GetFileName(folderPath));
      return isValid;
    }

    private bool IsValidFolderRenameRequest(DirectoryRenameRequestDto directoryRenameRequestDto)
    {
      bool isValid = directoryRenameRequestDto != null &&
                      !string.IsNullOrWhiteSpace(Path.GetDirectoryName(directoryRenameRequestDto.CurrentDirectoryPath)) && string.IsNullOrWhiteSpace(Path.GetFileName(directoryRenameRequestDto.CurrentDirectoryPath)) &&
                      !string.IsNullOrWhiteSpace(Path.GetDirectoryName(directoryRenameRequestDto.RenamedDirectoryPath)) && string.IsNullOrWhiteSpace(Path.GetFileName(directoryRenameRequestDto.RenamedDirectoryPath));

      return isValid;
    }

    private bool IsValidFolderCreateRequest(DirectoryCreateRequestDto directoryCreateRequestDto)
    {
      string[] invalidStrings = { "+", "%2B" };

      var directoryName = Path.GetDirectoryName(directoryCreateRequestDto.DirectoryPathToCreate); 
      bool isValid = directoryCreateRequestDto != null &&
                                                !string.IsNullOrWhiteSpace(directoryName) && 
                                                string.IsNullOrWhiteSpace(Path.GetFileName(directoryCreateRequestDto.DirectoryPathToCreate)) &&
                                                !invalidStrings.Any(invalidString => directoryName.Contains(invalidString));
      return isValid;
    }

    private bool IsValidMultiFileRequest(IEnumerable<Guid> filesIds)
    {
      bool isValid = filesIds.All(item => item != null && TryValidatingGuid(item.ToString(), out Guid resultingGuid));
      return isValid;
    }

    #endregion
  }
}
