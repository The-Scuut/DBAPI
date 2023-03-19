namespace DBAPI.Models;

using MySqlConnector;

public class DatabaseConfig
{
    public string Host { get; set; } = "localhost";
    public ushort Port { get; set; } = 3306;
    public string Database { get; set; } = "dbapi";
    public string DatabaseInternal { get; set; } = "dbapiinternal";
    public string Username { get; set; } = "root";
    public string Password { get; set; } = "";

    public bool IsDefault() => Host == "localhost" &&
                               Port == 3306 &&
                               Database == "dbapi" &&
                               Username == "root" &&
                               Password == "";

    public string GetConnectionString(bool internalDb = false)
    {
        MySqlConnectionStringBuilder builder = new()
        {
            Server = Host,
            Port = Port,
            Database = internalDb ? DatabaseInternal : Database,
            UserID = Username,
            Password = Password
        };
        return builder.ToString();
    }

    public string GetConnectionStringNoDatabase()
    {
        MySqlConnectionStringBuilder builder = new()
        {
            Server = Host,
            Port = Port,
            UserID = Username,
            Password = Password
        };
        return builder.ToString();
    }
}