using Fixit.Core.DataContracts;
using Fixit.Core.Storage.FileSystem.Managers;
using Fixit.Core.Storage.Storage.Table.Managers;

namespace Fixit.FileManagement.Lib.Models
{
  public class FileSystemCreate : OperationStatus
  {
    public ITableStorage TableStorage { get; set; }

    public IFileSystemClient FileSystemClient { get; set; }

  }
}