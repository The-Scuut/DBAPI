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

    [HttpGet("[controller]/getinfo")]
    public async Task<IActionResult> GetInfo()
    {
        var certhash = CurrentCertificateInfo.Certificate?.GetCertHash();
        var cert = certhash != null ? Convert.ToBase64String(certhash) : null;
        return Content(JsonConvert.SerializeObject(new
        {
            HttpsEnabled = CurrentCertificateInfo.Manager != null,
            SelfSigned = CurrentCertificateInfo.Manager?.IsSelfSigned ?? false,
            Certificate = cert ?? "",
        }));
    }
}