using Microsoft.EntityFrameworkCore;
using VideoEncoder.Database;
using VideoEncoder.Database.Models;
using VideoEncoder.Services;
using VideoEncoder.Services.Storage;

namespace VideoEncoder;

public class Worker(
    ILogger<Worker> logger,
    IServiceProvider provider) : BackgroundService
{
#if DEBUG
    private bool dummyVideoUploaded;
#endif

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<VideoEncoderDbContext>();
            var videoService = scope.ServiceProvider.GetRequiredService<VideoEncoderService>();
            var queueElements = context.QueueElements
                .Where(element => element.Status != Database.Enums.EStatus.FINISHED_SUCCESS)
                .Take(10)
                .ToList();
            
            if(queueElements.Count > 0)
            {
                foreach (var item in queueElements)
                {
                    logger.LogDebug("VideoEncoder - Enqueue item with id: {id}", item.Id);
                    await videoService.EnqueueAsync(item);
                }
                var processResults = await videoService.StartProcessingAsync(queueElements.Count);
                foreach (var result in processResults)
                {

                    var element = queueElements.Single(item => item.Id == result.Item1);
                    element.Status = result.Item2 
                        ? Database.Enums.EStatus.FINISHED_SUCCESS
                        : Database.Enums.EStatus.FINISHED_ERROR;
                }
                try
                {
                    await context.SaveChangesAsync();
                    logger.LogInformation("VideoEncoder - Database updated successfully");
                }
                catch (DbUpdateException ex)
                {
                    logger.LogError("Videoencoder - An error occured trying to update the database, Exception {0}", ex);
                }

            }
            else
            {
                logger.LogInformation("VideoEncoder - No Videos found to process");
#if DEBUG
                if (!dummyVideoUploaded)
                {
                    dummyVideoUploaded = await UploadDummyVideoAsync(
                        scope.ServiceProvider.GetRequiredService<IStorageService>(),
                        context,
                        stoppingToken);
                }
#endif
            }
            await Task.Delay(5000, stoppingToken);
        }
    }

#if DEBUG
    private async Task<bool> UploadDummyVideoAsync(IStorageService storageService, VideoEncoderDbContext context, CancellationToken stoppingToken)
    {
        var dummyVideoPath = Path.Combine(Path.GetTempPath(), "VideoEncoder", "dummy-video.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(dummyVideoPath)!);

        try
        {
            if (!File.Exists(dummyVideoPath))
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.StartInfo.ArgumentList.Add("-y");
                process.StartInfo.ArgumentList.Add("-f");
                process.StartInfo.ArgumentList.Add("lavfi");
                process.StartInfo.ArgumentList.Add("-i");
                process.StartInfo.ArgumentList.Add("color=c=black:s=320x240:d=1");
                process.StartInfo.ArgumentList.Add("-f");
                process.StartInfo.ArgumentList.Add("lavfi");
                process.StartInfo.ArgumentList.Add("-i");
                process.StartInfo.ArgumentList.Add("anullsrc=r=44100:cl=mono");
                process.StartInfo.ArgumentList.Add("-t");
                process.StartInfo.ArgumentList.Add("1");
                process.StartInfo.ArgumentList.Add("-c:v");
                process.StartInfo.ArgumentList.Add("libx264");
                process.StartInfo.ArgumentList.Add("-pix_fmt");
                process.StartInfo.ArgumentList.Add("yuv420p");
                process.StartInfo.ArgumentList.Add("-c:a");
                process.StartInfo.ArgumentList.Add("aac");
                process.StartInfo.ArgumentList.Add("-shortest");
                process.StartInfo.ArgumentList.Add(dummyVideoPath);

                process.Start();
                var error = await process.StandardError.ReadToEndAsync(stoppingToken);
                await process.WaitForExitAsync(stoppingToken);
                if (process.ExitCode != 0)
                {
                    logger.LogError("VideoEncoder - Failed to create dummy video: {Error}", error);
                    return false;
                }
            }

            var uploaded = await storageService.UploadAsync(dummyVideoPath, "development/dummy-video.mp4");
            if (uploaded)
            {
                logger.LogInformation("VideoEncoder - Uploaded development dummy video to S3");
                context.QueueElements.Add(new VideoQueue()
                {
                    Id = Guid.NewGuid(),
                    FileUrl = "development/dummy-video.mp4",
                    Status = Database.Enums.EStatus.NONE,
                    UploadTime = DateTime.UtcNow,
                    UserId = Guid.NewGuid(),
                    VideoName = Guid.NewGuid().ToString(),
                });
                await context.SaveChangesAsync();
            }

            return uploaded;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "VideoEncoder - Failed to upload development dummy video");
            return false;
        }
    }
#endif
}

