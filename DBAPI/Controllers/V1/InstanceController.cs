namespace DBAPI.Controllers.V1;

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

[Route("Application/V1/")]
[ApiController]
public class InstanceController : ControllerBase
{
    private readonly ILogger<InstanceController> _logger;

    public InstanceController(ILogger<InstanceController> logger)
    {
        _logger = logger;
    }

    [HttpPost("[controller]/getinfo")]
    public async Task<IActionResult> GetInfo()
    {
        return Content(JsonConvert.SerializeObject(new
        {
            HttpsEnabled = CurrentCertificateInfo.Manager != null,
            SelfSigned = CurrentCertificateInfo.Manager?.IsSelfSigned ?? false,
            Certificate = CurrentCertificateInfo.Certificate?.ToString() ?? "",
        }));
    }
}