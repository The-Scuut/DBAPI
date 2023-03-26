namespace DBAPI.Library
{
    using System;
    using DBAPI.Library.Models;

    public class APIClient
    {
        public string Host { get; }
        private readonly Guid _token;

        public APIClient(APIClientParameters parameters)
        {
            _ = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Host = parameters.UseSSL ? $"https://{parameters.Host}:{parameters.Port}" : $"http://{parameters.Host}:{parameters.Port}";
            if (!Uri.IsWellFormedUriString(Host, UriKind.Absolute))
                throw new ArgumentException("Invalid host format");
            if(!Guid.TryParse(parameters.Token, out _token))
                throw new ArgumentException("Invalid token format");
        }
    }
}