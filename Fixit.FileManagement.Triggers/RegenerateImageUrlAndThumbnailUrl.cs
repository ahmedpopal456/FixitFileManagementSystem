using Fixit.FileManagement.Lib.Managers;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fixit.Core.Storage.DataContracts.FileSystem.Events;

namespace Fixit.FileManagement.Triggers
{
  public class RegenerateImageUrlAndThumbnailUrl
  {
    private readonly IFileManager _fileManager;

    public RegenerateImageUrlAndThumbnailUrl(IFileManager fileManager)
    {    
      _fileManager = fileManager ?? throw new ArgumentNullException($"{nameof(RegenerateImageUrlAndThumbnailUrl)} expects a value for {nameof(fileManager)}... null argument was provided");
    }
     
    [FunctionName(nameof(RegenerateImageUrlAndThumbnailUrl))]
    public async Task RunAsync([EventGridTrigger] EventGridEvent eventGridEvent)
    {
      var cancellationToken = new CancellationToken();      
      var eventData = JsonConvert.DeserializeObject<RegenerateImageUrlEvent>(eventGridEvent.Data.ToString());      
      if (eventData != null && eventData.FilesToRegenerateUrls != null && eventData.FilesToRegenerateUrls.Any())
      {        
        var regenerateTasks = await _fileManager.RegenerateUrlsAsync(eventData.FilesToRegenerateUrls, cancellationToken);
        await Task.WhenAll(regenerateTasks);
      }
    }
  }
}
