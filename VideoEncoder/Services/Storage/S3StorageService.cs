using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Options;
using VideoEncoder.Settings;

namespace VideoEncoder.Services.Storage;

public class S3StorageService : IStorageService
{
    private readonly ILogger<S3StorageService> _logger;
    private readonly S3Settings _settings;
    public S3StorageService(ILogger<S3StorageService> logger, IOptions<S3Settings> options)
    {
        this._logger = logger;
        this._settings = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<bool> DownloadAsync(string source, string destination)
    {
        try
        {
            using var s3Client = CreateClient();
            if(IsLocalS3())
                await EnsureBucketExistsAsync(s3Client, this._settings.BucketName);

            var destinationDirectory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            using var transferUtility = new TransferUtility(s3Client);
            await transferUtility.DownloadAsync(new TransferUtilityDownloadRequest
            {
                BucketName = this._settings.BucketName,
                Key = NormalizeKey(source),
                FilePath = destination
            });

            this._logger.LogInformation("Downloaded S3 object {ObjectKey} to {Destination}", source, destination);
            return true;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Failed to download S3 object {ObjectKey} to {Destination}", source, destination);
            return false;
        }
    }

    public async Task<bool> UploadAsync(string localSourcePath, string destinationKeyOrPath)
    {
        try
        {
            using var s3Client = CreateClient();
            if(IsLocalS3())
            {
                await EnsureBucketExistsAsync(s3Client, this._settings.BucketName);

                var putRequest = new PutObjectRequest
                {
                    BucketName = this._settings.BucketName,
                    Key = NormalizeKey(destinationKeyOrPath),
                    FilePath = localSourcePath,
                    UseChunkEncoding = false
                };

                await s3Client.PutObjectAsync(putRequest);
            }
            else
            {               
                using var transferUtility = new TransferUtility(s3Client);
                await transferUtility.UploadAsync(new TransferUtilityUploadRequest
                {
                    BucketName = this._settings.BucketName,
                    Key = NormalizeKey(destinationKeyOrPath),
                    FilePath = localSourcePath
                });
            }
            this._logger.LogInformation("Uploaded {Source} to S3 object {ObjectKey}", localSourcePath, destinationKeyOrPath);
            return true;

        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Failed to upload {Source} to S3 object {ObjectKey}", localSourcePath, destinationKeyOrPath);
            return false;
        }
    }

    private IAmazonS3 CreateClient()
    {
        return new AmazonS3Client(this._settings.AccessKey, this._settings.SecretKey, new AmazonS3Config
        {
            ServiceURL = this._settings.ServiceUrl,
            ForcePathStyle = true,
            UseHttp = IsLocalS3(),
            AuthenticationRegion = this._settings.Region
        });
    }

    private bool IsLocalS3() => this._settings.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeKey(string key) => key.Replace(Path.DirectorySeparatorChar, '/').TrimStart('/');

    private async Task EnsureBucketExistsAsync(IAmazonS3 s3Client, string bucketName)
    {
        try
        {
            await s3Client.GetBucketLocationAsync(new GetBucketLocationRequest() { BucketName = bucketName });
            this._logger.LogDebug($"Bucket '{bucketName}' existiert bereits.");
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            this._logger.LogDebug($"Bucket '{bucketName}' existiert nicht. Wird erstellt...");
            await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });
            this._logger.LogDebug("Bucket erstellt.");
        }
    }
}