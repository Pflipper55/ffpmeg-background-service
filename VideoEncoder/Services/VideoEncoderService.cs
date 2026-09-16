using System.Diagnostics;
using System.Threading.Channels;
using VideoEncoder.Database.Models;
using VideoEncoder.Services.Storage;

namespace VideoEncoder.Services;

public class VideoEncoderService
{
    private readonly ILogger<VideoEncoderService> _logger;
    private readonly IStorageService _storageService;
    private readonly Channel<VideoQueue> _channel = Channel.CreateUnbounded<VideoQueue>();

    public VideoEncoderService(ILogger<VideoEncoderService> logger, IStorageService storageService)
    {
        _logger = logger;
        _storageService = storageService;
    }

    public async Task EnqueueAsync(VideoQueue job) => await _channel.Writer.WriteAsync(job);

    public async Task<List<Tuple<Guid,bool>>> StartProcessingAsync(int jobCount)
    {
        var x = await ProcessJobsAsync(jobCount);
        return x;
    }

    private async Task<List<Tuple<Guid, bool>>> ProcessJobsAsync(int jobCount)
    {
        var result = new List<Tuple<Guid, bool>>();
        for (var index = 0; index < jobCount; index++)
        {
            var job = await _channel.Reader.ReadAsync();
            var download = Path.Combine(Environment.CurrentDirectory, job.VideoName)+".mp4";
            var successDownload = await _storageService.DownloadAsync(job.FileUrl, download);
            if(successDownload)
            {
                this._logger.LogDebug("Start processing video with id: {0}", job.Id);
                var ffpmegResult = await RunFFmpegAsync(download, job.Id);
                this._logger.LogDebug("Video with id: {0} processing result {1}", job.Id, ffpmegResult);
                result.Add(new Tuple<Guid, bool>(job.Id, ffpmegResult));
            }
            else
            {
                this._logger.LogError("Video with id: {0} had an error during download", job.Id);
                result.Add(new Tuple<Guid, bool>(job.Id, false));
            }
        }
        return result;
    }

    private async Task<bool> RunFFmpegAsync(string input, Guid jobId)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "VideoEncoder", jobId.ToString());
        Directory.CreateDirectory(Path.Combine(outputDirectory, "v0"));
        Directory.CreateDirectory(Path.Combine(outputDirectory, "v1"));
        Directory.CreateDirectory(Path.Combine(outputDirectory, "v2"));

        var segmentPattern = Path.Combine(outputDirectory, "v%v", "segment_%03d.ts");
        var playlistPattern = Path.Combine(outputDirectory, "v%v", "index.m3u8");
        var arguments =
            $"-i \"{input}\" " +
            "-filter_complex " +
            "\"[0:v]split=3[v1080][v720][v480];" +
            "[v1080]scale=1920:1080:force_original_aspect_ratio=decrease," +
            "pad=1920:1080:(ow-iw)/2:(oh-ih)/2[v1080out];" +
            "[v720]scale=1280:720:force_original_aspect_ratio=decrease," +
            "pad=1280:720:(ow-iw)/2:(oh-ih)/2[v720out];" +
            "[v480]scale=854:480:force_original_aspect_ratio=decrease," +
            "pad=854:480:(ow-iw)/2:(oh-ih)/2[v480out]\" " +
            "-map \"[v1080out]\" -map 0:a:0? " +
            "-map \"[v720out]\" -map 0:a:0? " +
            "-map \"[v480out]\" -map 0:a:0? " +
            "-c:v libx264 -preset fast -profile:v main " +
            "-b:v:0 5000k -maxrate:v:0 5350k -bufsize:v:0 7500k " +
            "-b:v:1 2800k -maxrate:v:1 2996k -bufsize:v:1 4200k " +
            "-b:v:2 1400k -maxrate:v:2 1498k -bufsize:v:2 2100k " +
            "-c:a aac -b:a 128k -ac 2 " +
            "-f hls -hls_time 6 -hls_playlist_type vod " +
            "-hls_flags independent_segments " +
            $"-hls_segment_filename \"{segmentPattern}\" " +
            "-master_pl_name master.m3u8 " +
            "-var_stream_map \"v:0,a:0 v:1,a:1 v:2,a:2\" " +
            $"\"{playlistPattern}\"";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var result = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("FFmpeg failed for job {JobId}: {Error}", jobId, result);
            return false;
        }

        var destinationDirectory = Path.Combine(Environment.CurrentDirectory, "Videos", jobId.ToString());
        foreach (var file in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(outputDirectory, file);
            var destination = Path.Combine(destinationDirectory, relativePath);
            var uploadResult = await _storageService.UploadAsync(file, destination);
            if (!uploadResult)
            {
                return false;
            }
        }

        Directory.Delete(outputDirectory, recursive: true);
        return true;
    }
}