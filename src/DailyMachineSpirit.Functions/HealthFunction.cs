using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace DailyMachineSpirit.Functions;

/// <summary><c>/healthz</c>: whether the app answers, and which commit it runs (the deploys' smoke tests wait for it).</summary>
public class HealthFunction
{
    [Function("Health")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "healthz")] HttpRequest request)
        => new OkObjectResult(new { status = "healthy", version = AppVersion.Short });
}
