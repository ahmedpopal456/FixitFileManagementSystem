using Fixit.Core.DataContracts;
using Fixit.FileManagement.Lib.Models;
using System.Threading;
using System.Threading.Tasks;
using Fixit.Core.Storage.DataContracts.FileSystem.Files;
using Fixit.Core.Storage.FileSystem.Managers;
using Fixit.Core.Storage.Storage.Table.Managers;

namespace Fixit.FileManagement.Lib.Managers
{
  public interface IFileSystemManager
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<FileSystemCreate> CreateOrGetFileSystemAsync(string fileSystemName, long fileSystemId, CancellationToken cancellationToken);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<OperationStatus> DeleteFileSystemAsync(string fileSystemName, long fileSystemId, CancellationToken cancellationToken);

    /// <summary>
    /// Get the Table storage dans the File system client
    /// </summary>
    /// <param name="fileSystemId">File system id</param>
    /// <param name="fileSystemName">File system name</param>
    /// <param name="tableEntity">The table storage</param>
    /// <param name="fileSystemClient">The file system client</param>
    void GetTableEntityAndFileSystemClient(long fileSystemId, string fileSystemName, out ITableStorage tableEntity, out IFileSystemClient fileSystemClient);

    /// <summary>
    /// Regenerate and file url while making sure the FileSystem exist
    /// </summary>
    /// <param name="entityName">File system name</param>
    /// <param name="entityId">File system id</param>
    /// <param name="fileId">The file id</param>
    /// <param name="expirationTime">The epxiration time</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    /// <param name="url">The url</param>
    /// <returns></returns>
    Task<ImageUrlDto> GenerateImageUrlAsync(string entityName, long entityId, string fileId, int? expirationTime, CancellationToken cancellationToken, string url = null);
  }
}