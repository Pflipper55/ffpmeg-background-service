using VideoEncoder.Database.Enums;

namespace VideoEncoder.Database.Models;

public class VideoQueue
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public EStatus Status{ get; set; } = EStatus.NONE;

    public string VideoName { get; set; } = string.Empty;

    public DateTime UploadTime { get; set; }

    public Guid UserId { get; set; } = Guid.Empty;

    /// <summary>
    /// Name of Bucket or Filesystem URI
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;
}