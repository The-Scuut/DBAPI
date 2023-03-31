﻿namespace DBAPI.Controllers.V1;

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
        var content = Content($"[{string.Join(';', Messages.ContainsKey(channel) ? Messages[channel] : Array.Empty<string>())}]");
        Messages[channel] = Array.Empty<string>();
        return content;
    }

    [HttpGet("[controller]/peek/{channel}")]
    public async Task<IActionResult> Peek(string channel)
    {
        return Content($"[{string.Join(';', Messages.ContainsKey(channel) ? Messages[channel] : Array.Empty<string>())}]");
    }

    [HttpPost("[controller]/send/{channel}")]
    public async Task<IActionResult> Send(string channel, [FromBody]object data)
    {
        if (string.IsNullOrWhiteSpace(channel))
            return BadRequest("Channel may not be null or empty");
        if (string.IsNullOrWhiteSpace(data?.ToString()))
            return BadRequest("Content may not be null or empty");
        if (!Messages.ContainsKey(channel))
        {
            if (Messages.Keys.Count >= 50)
                return BadRequest("Too many channels (max 50)");
            Messages[channel] = Array.Empty<string>();
        }
        string[]? messagesToAppend;
        try
        {
            messagesToAppend = data.ToString()!.TrimStart('[').TrimEnd(']').ReplaceLineEndings("").Replace(" ", "").Split(';');
        }
        catch (Exception e)
        {
            return BadRequest("Could not serialize, exception: " + e.Message);
        }
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