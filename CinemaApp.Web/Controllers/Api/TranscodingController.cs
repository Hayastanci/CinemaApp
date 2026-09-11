using CinemaApp.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CinemaApp.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
public class TranscodingController : ControllerBase
{
    private readonly ITranscodingStatusService _statusService;

    public TranscodingController(ITranscodingStatusService statusService)
    {
        _statusService = statusService;
    }

    [HttpGet("status/{movieId:int}")]
    public IActionResult GetStatus(int movieId)
    {
        var progress = _statusService.GetProgress(movieId);
        if (progress == null)
        {
            return Ok(new
            {
                movieId,
                status = "Idle",
                percent = 100,
                currentStep = "Ready",
                completedQualities = new string[] { }
            });
        }

        return Ok(progress);
    }

    [HttpGet("active")]
    public IActionResult GetActive()
    {
        var all = _statusService.GetAll()
            .Where(p => p.Value.Status == "Queued" || p.Value.Status == "Processing")
            .Select(p => p.Value)
            .ToList();

        return Ok(all);
    }
}
