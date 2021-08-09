using System;
using System.IO;

namespace Fixit.FileManagement.Lib.Utilities
{
  public static class EmpowerPathHelper
  {
    public static string AddGuidToFilePath(string filePath, Guid? guid = null)
    {
      var result = filePath; 

      if (string.IsNullOrWhiteSpace(filePath))
      {
        return result;
      }

      if(guid == null)
      {
        guid = Guid.NewGuid();
      }
      
      result = $"{Path.Combine(Path.GetDirectoryName(filePath),Path.GetFileNameWithoutExtension(filePath))}_{guid}{Path.GetExtension(filePath)}";
      return result;
    }
  }
}
