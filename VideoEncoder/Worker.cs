using Microsoft.EntityFrameworkCore;
using VideoEncoder.Database;
using VideoEncoder.Database.Models;
using VideoEncoder.Services;

namespace VideoEncoder;

public class Worker(ILogger<Worker> logger , IServiceProvider provider) : BackgroundService
{
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
                // await context.QueueElements.AddAsync(new VideoQueue()
                // {
                //    Id = Guid.NewGuid(),
                //    Status = Database.Enums.EStatus.NONE,
                //    UserId = Guid.NewGuid(),
                //    VideoName = Guid.NewGuid().ToString(),
                //    FileUrl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "test.mp4")
                // });
                // await context.SaveChangesAsync();
            }
            await Task.Delay(5000, stoppingToken);
        }
    }
}
