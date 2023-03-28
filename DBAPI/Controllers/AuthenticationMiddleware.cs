namespace DBAPI.Controllers;

using System.Net;
using System.Text;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        if (context.Request.Path == "/")
        {
            await _next.Invoke(context);
            return;
        }

        if (!ConfigManager.APIConfig.RequireTokenForLocalhost && Equals(context.Connection.LocalIpAddress, IPAddress.Loopback))
        {
            await _next.Invoke(context);
            return;
        }
        string token = context.Request.Headers["token"];
        if (token != null && !string.IsNullOrEmpty(token))
        {
            var tokenLength = token.Length;
            var tokenString = token;
            bool IsValidBase64(out string decrypted)
            {
                Span<byte> buffer = new Span<byte>(new byte[tokenLength]);
                decrypted = string.Empty;
                if (!Convert.TryFromBase64String(tokenString, buffer, out _))
                    return false; 
                decrypted = Encoding.UTF8.GetString(buffer);
                return true;
            }

            if (IsValidBase64(out token) && !string.IsNullOrWhiteSpace(token) && await TokenManager.IsValid(token))
            {
                await _next.Invoke(context);
            }
            else
            {
                context.Response.StatusCode = 401;
            }
        }
        else
        {
            context.Response.StatusCode = 401;
        }
    }
}