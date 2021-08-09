using Fixit.Core.DataContracts;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fixit.Core.Storage.DataContracts.FileSystem.Files;
using Fixit.Core.Storage.DataContracts.FileSystem.Files.Requests;
using Fixit.Core.Storage.DataContracts.FileSystem.Files.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Fixit.FileManagement.Lib.Managers
{
  public interface IFileManager
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="iFileId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<OperationStatus> DeleteFileAsync(string fileSystemName, long fileSystemId, Guid iFileId, CancellationToken cancellationToken);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="iFileId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<FileDownloadResponseDto> DownloadFileAsync(string fileSystemName, long fileSystemId, Guid iFileId, CancellationToken cancellationToken);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="fileRequestDtos"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<FileDownloadResponseDto>> DownloadFilesAsync(string fileSystemName, long fileSystemId, IEnumerable<Guid> fileRequestDtos, CancellationToken cancellationToken);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="iFileId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<FileStreamResult> GetFileAsync(string fileSystemName, long fileSystemId, Guid iFileId, CancellationToken cancellationToken);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="iFileId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<FileResponseDto> GetFileInfoAsync(string fileSystemName, long fileSystemId, Guid iFileId, CancellationToken cancellationToken);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="fileIds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<FileResponseDto>> GetFilesInfoAsync(string fileSystemName, long fileSystemId, IEnumerable<Guid> fileIds, CancellationToken cancellationToken);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="iFileId"></param>
    /// <param name="fileRenameRequestVm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<OperationStatus> RenameFileAsync(string fileSystemName, long fileSystemId, Guid iFileId, FileRenameRequestDto fileRenameRequestVm, CancellationToken cancellationToken);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="fileId"></param>
    /// <param name="fileMetadataSummary"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<FileMetadataDto> SetFileMetadataAsync(string fileSystemName, long fileSystemId, Guid fileId, FileMetadataSummary fileMetadataSummary, CancellationToken cancellationToken);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="fileUploadRequestVm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<FileUploadResponseDto> UploadFileAsync(string fileSystemName, long fileSystemId, FileUploadRequestDto fileUploadRequestVm, CancellationToken cancellationToken);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="fileUploadsRequestVm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<FileUploadResponseDto>> UploadFilesAsync(string fileSystemName, long fileSystemId, MultiFileUploadRequestDto fileUploadsRequestVm, CancellationToken cancellationToken);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="files"></param>
    /// <param name="cancellationToken"></param>  
    /// <returns></returns>
    Task<IEnumerable<Task>> RegenerateUrlsAsync(IEnumerable<FileToRegenerateUrlDto> files, CancellationToken cancellationToken);
  }
}