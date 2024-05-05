using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace InvisibilityTestClient;

public class Test
{
    private readonly ILogger<Test> _logger;

    public Test(ILogger<Test> logger)
    {
        _logger = logger;
    }
    
    public async Task LongTask(TimeSpan duration, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Task started");
        while (duration >= TimeSpan.Zero)
        {
            // cancellationToken.ThrowIfCancellationRequested();

            var waitInterval = TimeSpan.FromSeconds(10);
            
            _logger.LogInformation(duration.ToString());

            _logger.LogInformation("Waiting...");
            // await Task.Delay(waitInterval, cancellationToken);
            await Task.Delay(waitInterval);
            duration -= waitInterval;
            _logger.LogInformation("Wait finished");
        }
        _logger.LogInformation("Task finished");
    }
}