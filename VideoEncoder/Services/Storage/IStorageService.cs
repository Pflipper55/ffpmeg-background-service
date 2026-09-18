namespace VideoEncoder.Services.Storage;

public interface IStorageService
{
    /// <summary>
    /// Gets the data from the source
    /// </summary>
    /// <param name="source">the path / url of the source file</param>
    /// <param name="destination">Downloadpath of the file at the maschine</param>
    /// <returns></returns>
    Task<bool> DownloadAsync(string source, string destination);

    /// <summary>
    /// Upload the file to the destination
    /// </summary>
    /// <param name="localSourcePath">Path where the processed file is</param>
    /// <param name="destinationKeyOrPath">Path / Url of the file destination</param>
    /// <returns></returns>
    Task<bool> UploadAsync(string localSourcePath, string destinationKeyOrPath);
}