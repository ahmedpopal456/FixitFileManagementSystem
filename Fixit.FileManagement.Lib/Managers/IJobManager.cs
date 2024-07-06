using Empower.Core.DataContracts.Systems.File.Jobs.Requests;
using Empower.Core.DataContracts.Systems.File.Jobs.Responses;
using System.Threading;
using System.Threading.Tasks;

namespace Empower.FileManagement.Lib.Managers
{
  public interface IJobManager
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileDownloadJobRequestVm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<FileDownloadJobResponseDto> CreateFileDownloadJob(FileDownloadJobRequestDto fileDownloadJobRequestVm, CancellationToken cancellationToken);
  }
}