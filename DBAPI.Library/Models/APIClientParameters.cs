namespace DBAPI.Library.Models
{
    public class APIClientParameters
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Token { get; set; }
        public bool UseSSL { get; set; } = true;
    }
}