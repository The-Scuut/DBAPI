namespace DBAPI.Controllers;

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
        string token = context.Request.Headers["token"];
        if (token != null && !string.IsNullOrEmpty(token))
        {
            bool IsValidBase64(out string decrypted)
            {
                Span<byte> buffer = new Span<byte>(new byte[token.Length]);
                decrypted = string.Empty;
                if (!Convert.TryFromBase64String(token, buffer, out _))
                    return false; 
                decrypted = Encoding.UTF8.GetString(buffer);
                return true;
            }

            if (IsValidBase64(out token) && await TokenManager.IsValid(token))
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