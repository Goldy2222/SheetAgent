using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class Worker : BackgroundService
{
    private readonly PipelineService _pipeline;
    private readonly ILogger<Worker> _logger;

    public Worker(PipelineService pipeline, ILogger<Worker> logger)
    {
        _pipeline = pipeline;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Pipeline running at: {time}", DateTimeOffset.Now);
            await _pipeline.RunAsync();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken); // adjust schedule
        }
    }
}