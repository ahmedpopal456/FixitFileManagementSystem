using Fixit.Core.DataContracts.Events.EventGrid.Managers;

namespace Fixit.FileManagement.Lib
{
  public delegate IEventGridTopicServiceClient EventGridTopicServiceClientResolver(string key);
  
  public class FmsAssemblyInfo
  {
    public static readonly string DataVersion = "1.0";
  }
}
