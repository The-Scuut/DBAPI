namespace DBAPI.Controllers.V1;

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

[Route("Application/V1/")]
[ApiController]
public class MessagingController : ControllerBase
{
    private readonly ILogger<MessagingController> _logger;
    public static readonly Dictionary<string, string[]> Messages = new();

    public MessagingController(ILogger<MessagingController> logger)
    {
        _logger = logger;
    }

    [HttpGet("[controller]/list")]
    public async Task<IActionResult> List()
    {
        return Content(JsonConvert.SerializeObject(Messages.Keys.ToArray()));
    }

    [HttpGet("[controller]/read/{channel}")]
    public async Task<IActionResult> Read(string channel)
    {
        var content = Content(JsonConvert.SerializeObject(Messages.ContainsKey(channel) ? Messages[channel] : Array.Empty<string>()));
        Messages[channel] = Array.Empty<string>();
        return content;
    }

    [HttpGet("[controller]/peek/{channel}")]
    public async Task<IActionResult> Peek(string channel)
    {
        return Content(JsonConvert.SerializeObject(Messages.ContainsKey(channel) ? Messages[channel] : Array.Empty<string>()));
    }

    [HttpPost("[controller]/send/{channel}")]
    public async Task<IActionResult> Send(string channel, [FromBody]object data)
    {
        if (data is not string dataString)
            return BadRequest("Invalid data");
        if (!Messages.ContainsKey(channel))
        {
            if (Messages.Keys.Count >= 50)
                return BadRequest("Too many channels (max 50)");
            Messages[channel] = Array.Empty<string>();
        }
        var messagesToAppend = JsonConvert.DeserializeObject<string[]>(dataString);
        if (messagesToAppend == null)
            return BadRequest("Data null");
        if (messagesToAppend.Length + Messages[channel].Length > 100)
            return BadRequest("Too many messages to append (max 100 per channel)");
        Messages[channel] = Messages[channel].Concat(messagesToAppend).ToArray();
        return Ok();
    }

    [HttpGet("[controller]/clear/{channel}")]
    public async Task<IActionResult> Clear(string channel)
    {
        if (!Messages.ContainsKey(channel))
            return BadRequest("Channel does not exist");
        Messages.Remove(channel);
        return Ok();
    }
}