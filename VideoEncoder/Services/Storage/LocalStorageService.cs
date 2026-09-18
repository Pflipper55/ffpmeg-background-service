namespace VideoEncoder.Services.Storage;

public class LocalStorageService : IStorageService
{
    private readonly ILogger<LocalStorageService> _logger;
    public LocalStorageService(ILogger<LocalStorageService> logger)
    {
        this._logger = logger;
    }

    public Task<bool> DownloadAsync(string source, string destination)
    {
        try
        {
            File.Copy(source, destination, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> UploadAsync(string localSourcePath, string destinationKeyOrPath)
    {
        try
        {
            var destinationDirectory = Path.GetDirectoryName(destinationKeyOrPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(localSourcePath, destinationKeyOrPath, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }
}