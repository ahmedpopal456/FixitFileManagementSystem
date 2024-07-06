using Empower.Core.Caching.Clients;
using Empower.Core.DataContracts.Systems.File.Jobs.Requests;
using Empower.Core.DataContracts.Systems.File.Jobs.Responses;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Empower.FileManagement.Lib.Managers.Internal
{
  internal class JobManager : IJobManager
  {
    private readonly string _streamName = nameof(JobManager);
    private readonly TimeSpan _jobTimeoutInHours;

    private readonly IConfiguration _configuration;

    public JobManager(IConfiguration configuration)
    {
      _configuration = configuration ?? throw new ArgumentNullException($"{nameof(JobManager)} expects a value for {nameof(configuration)}... null argument was provided");

      if (!Int32.TryParse(_configuration.GetSection("RDS-Timeout").Value, out int wResult))
      {
        throw new ApplicationException("Unable to connect to the redis client...");
      }

      _jobTimeoutInHours = TimeSpan.FromMilliseconds(wResult);
    }

    public async Task<FileDownloadJobResponseDto> CreateFileDownloadJob(FileDownloadJobRequestDto fileDownloadJobRequestDto, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _ = fileDownloadJobRequestDto ?? throw new ArgumentNullException();      

      var jobId = new Guid();
      var serializedRequest = JsonSerializer.Serialize(fileDownloadJobRequestDto);

      var redisEntryId = await _redisClient.CreateItemInStreamAsync(_streamName, 
                                                                    new KeyValuePair<string, string>(jobId.ToString(),serializedRequest), 
                                                                    _jobTimeoutInHours);
      var response = new FileDownloadJobResponseDto()
      {
        JobId = jobId,
        CreatedTimestampUTC = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
        FilePathsRequested = fileDownloadJobRequestDto.FilePathsRequested
      };

      return response;
    }
  }
}