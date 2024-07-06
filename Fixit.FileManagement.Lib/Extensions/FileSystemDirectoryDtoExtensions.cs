using System.Collections.Generic;
using System.Linq;
using Fixit.Core.Storage.DataContracts.FileSystem.Directories;
using Fixit.Core.Storage.DataContracts.FileSystem.Files;

namespace Fixit.FileManagement.Lib.Extensions
{
  public static class FileSystemDirectoryDtoExtensions
  {
    public static IEnumerable<FileSystemFileDto> ObtainFilesFromDirectory(this FileSystemDirectoryDto fileSystemDirectoryDto)
    {
      var files = fileSystemDirectoryDto.DirectoryItems != null ? fileSystemDirectoryDto.DirectoryItems.ToList() : new List<FileSystemFileDto>();

      foreach (var item in fileSystemDirectoryDto.Directories)
      {
        files.AddRange(ObtainFilesFromDirectory(item));
      }

      return files;
    }
  }
}
