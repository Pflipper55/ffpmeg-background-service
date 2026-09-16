using Microsoft.EntityFrameworkCore;
using VideoEncoder;
using VideoEncoder.Database;
using VideoEncoder.Services;
using VideoEncoder.Services.Storage;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging();
builder.Services.AddDbContext<VideoEncoderDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            // Enable retry on failure for transient errors
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);

            // Set command timeout for long-running queries
            npgsqlOptions.CommandTimeout(60);
        }));


builder.Services.AddTransient<IStorageService, LocalStorageService>();
builder.Services.AddSingleton<VideoEncoderService>();
       
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
