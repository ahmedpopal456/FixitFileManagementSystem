using Fixit.Core.DataContracts;
using System.Threading;
using System.Threading.Tasks;
using Fixit.Core.Storage.DataContracts.FileSystem.Directories;
using Fixit.Core.Storage.DataContracts.FileSystem.Directories.Requests;

namespace Fixit.FileManagement.Lib.Managers
{
  public interface IDirectoryManager
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="directoryCreateRequestVm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<OperationStatus> CreateDirectoryAsync(string fileSystemName, string fileSystemId, DirectoryCreateRequestDto directoryCreateRequestVm, CancellationToken cancellationToken);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="iFolderPath"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<OperationStatus> DeleteDirectoryAsync(string fileSystemName, string fileSystemId, string iFolderPath, CancellationToken cancellationToken);
   
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="iFolderPath"></param>
    /// <param name="iIgnorePrefix"></param>
    /// <param name="iIncludeItems"></param>
    /// <returns></returns>
    FileSystemDirectoryItemsDto GetDirectoryItems(string fileSystemName, string fileSystemId, string iFolderPath, bool iIgnorePrefix = false, bool iIncludeItems = true);
   
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="iFolderPath"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="iIgnorePrefix"></param>
    /// <param name="iIncludeItems"></param>
    /// <returns></returns>
    Task<FileSystemDirectoryItemsDto> GetDirectoryItemsAsync(string fileSystemName, string fileSystemId, string iFolderPath, CancellationToken cancellationToken, bool iIgnorePrefix = false, bool iIncludeItems = true);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="iIncludeItems"></param>
    /// <param name="singleLevel"></param>
    /// <returns></returns>
    FileSystemRootDirectoryDto GetDirectoryStructure(string fileSystemName, string fileSystemId, string folderPath, bool iIncludeItems = false, bool singleLevel = false);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="iIncludeItems"></param>
    /// <param name="singleLevel"></param>
    /// <returns></returns>
    Task<FileSystemRootDirectoryDto> GetDirectoryStructureAsync(string fileSystemName, string fileSystemId, string folderPath, CancellationToken cancellationToken, bool iIncludeItems = false, bool singleLevel = false);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileSystemName"></param>
    /// <param name="fileSystemId"></param>
    /// <param name="directoryRenameRequestVm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<OperationStatus> RenameDirectoryAsync(string fileSystemName, string fileSystemId, DirectoryRenameRequestDto directoryRenameRequestVm, CancellationToken cancellationToken);
  }
}