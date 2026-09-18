namespace VideoEncoder.Services.Storage;

public class S3StorageService : IStorageService
{
    private readonly ILogger<S3StorageService> _logger;
    public S3StorageService(ILogger<S3StorageService> logger)
    {
        this._logger = logger;
    }

    public Task<bool> DownloadAsync(string source, string destination)
    {
        throw new NotImplementedException();
    }

    public Task<bool> UploadAsync(string localSourcePath, string destinationKeyOrPath)
    {
         throw new NotImplementedException();
    }
}