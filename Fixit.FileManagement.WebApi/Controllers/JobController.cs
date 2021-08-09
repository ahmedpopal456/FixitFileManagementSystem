using Empower.Core.DataContracts.Systems.File.Jobs.Requests;
using Empower.Core.DataContracts.Systems.File.Jobs.Responses;
using Empower.Core.Security.Local;
using Empower.Core.Security.Local.Attributes;
using Empower.FileManagement.Lib.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Empower.FileManagement.WebApi.Controllers
{
  [Route("api/Jobs")]
  [Authorize("Permission")]
  public class JobController : ControllerBase
  {
    private readonly ILogger<JobController> _logger;
    private readonly IJobManager _jobManager;

    public JobController(ILogger<JobController> logger,
                         IJobManager jobManager)
    {
      _logger = logger ?? throw new ArgumentNullException($"{nameof(JobController)} expects a value for {nameof(logger)}... null argument was provided");
      _jobManager = jobManager ?? throw new ArgumentNullException($"{nameof(JobController)} expects a value for {nameof(jobManager)}... null argument was provided");
    }

    [HttpPost("Files/Download")]
    [Permission(PermissionDefinition.ViewFiles)]
    public async Task<IActionResult> CreateDownloadFilesJobAsync([FromBody] FileDownloadJobRequestDto fileDownloadJobRequestVm, CancellationToken cancellationToken)
    {
      if (!IsValidDownloadJobRequest(fileDownloadJobRequestVm))
      {
        return BadRequest($"One or more requested files specified in {nameof(fileDownloadJobRequestVm)} was invalid...");
      }

      var createdJobResponse = await _jobManager.CreateFileDownloadJob(fileDownloadJobRequestVm, cancellationToken);
      if (createdJobResponse == null)
      {
        return NotFound();
      }

      return Ok(createdJobResponse);
    }

    #region Helper Methods

    private bool IsValidDownloadJobRequest(FileDownloadJobRequestDto fileDownloadRequestVm)
    {
      bool isValid = !(fileDownloadRequestVm == null || fileDownloadRequestVm.FilePathsRequested.Any(item => string.IsNullOrWhiteSpace(item)));

      return isValid;
    }
    #endregion
  }
}