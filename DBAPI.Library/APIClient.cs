namespace DBAPI.Library
{
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DBAPI.Library.Models;

    public class APIClient : IDisposable
    {
        public string Host { get; }
        private readonly Guid _token;
        private HttpClient _client;

        public APIClient(APIClientParameters parameters)
        {
            _ = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Host = parameters.UseSSL
                ? $"https://{parameters.Host}:{parameters.Port}"
                : $"http://{parameters.Host}:{parameters.Port}";
            if (!Uri.IsWellFormedUriString(Host, UriKind.Absolute))
                throw new ArgumentException("Invalid host format");
            if (parameters.Token == "null" || string.IsNullOrWhiteSpace(parameters.Token))
                _token = Guid.Empty;
            else
            {
                if (!Guid.TryParse(parameters.Token, out _token))
                    throw new ArgumentException("Invalid token format");
            }
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("token", Convert.ToBase64String(Encoding.UTF8.GetBytes(_token.ToString())));
        }

        public void Connect()
        {
            var getInfoClient = new HttpClient(new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            });
            try
            {
                var serverInfo = getInfoClient.GetAsync(Host+"/Application/V1/Instance/getinfo").GetAwaiter().GetResult();
                if (!serverInfo.IsSuccessStatusCode)
                    throw new HttpRequestException("Connection unsuccessful, server returned " + serverInfo.StatusCode);
                var stringInfo = serverInfo.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                Console.WriteLine(stringInfo);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Could not connect to server", e);
            }
            finally
            {
                getInfoClient.Dispose();
            }
        }

        public readonly Action<string> OnMessageReceived = (s) => { };

        public void Dispose()
        {
            _client?.Dispose();
        }

        public void ListenToMessages(float interval = 3)
        {
            Action<string> callback = (s) => { OnMessageReceived(s);};
            Task.Run(() => ListenToMessagesAsync(CancellationToken.None, callback, interval));
        }

        private async Task ListenToMessagesAsync(CancellationToken token, Action<string> callback, float interval)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), token);
                
            }
        }
    }
}