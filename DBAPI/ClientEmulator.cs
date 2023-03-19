namespace DBAPI;

using System.Text;

public class ClientEmulator
{
    private static HttpClient _client = new HttpClient(new HttpClientHandler()
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }
    });
    private const string AppUrl = "http://localhost:80";
    static ClientEmulator()
    {
        _client.DefaultRequestHeaders.Add("token", Convert.ToBase64String(Encoding.UTF8.GetBytes(TokenManager.SessionToken.ToString())));
    }

    public static async Task<HttpResponseMessage> GetAsync(string url)
    {
        return await _client.GetAsync(AppUrl+url);
    }

    public static async Task<HttpResponseMessage> GetAsyncExtern(string url)
    {
        return await _client.GetAsync(url);
    }

    public static async Task<HttpResponseMessage> PostAsync(string url, string jsonContent)
    {
        HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        return await _client.PostAsync(AppUrl+url, content);
    }
}