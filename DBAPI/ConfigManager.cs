namespace DBAPI;

using DBAPI.Models;
using Newtonsoft.Json;

public static class ConfigManager
{
    public const string DBConfigPath = "dbconfig.json";
    public const string APIConfigPath = "apiconfig.json";
    public static DatabaseConfig DBConfig { get; private set; }
    public static APIConfig APIConfig { get; private set; }
    public static bool EnsureConfigsExists()
    {
        bool isDefault = false;
        if (!File.Exists(DBConfigPath))
        {
            File.WriteAllText(DBConfigPath, JsonConvert.SerializeObject(new DatabaseConfig(), Formatting.Indented));
            isDefault = true;
        }
        else
        {
            try
            {
                var deserializedConfig = JsonConvert.DeserializeObject<DatabaseConfig>(File.ReadAllText(DBConfigPath));
                DBConfig = deserializedConfig ?? throw new ArgumentException("File is empty.");
                isDefault = DBConfig.IsDefault();
            }
            catch (Exception e)
            {
                ConsoleUtils.WriteLine("There is a problem with your database config: " + e);
                var regen = ConsoleUtils.GetUserConfirmation("Would you like to reset your config? [Y/N]", ConsoleColor.Red, false);
                if (regen)
                {
                    File.WriteAllText(DBConfigPath, JsonConvert.SerializeObject(new DatabaseConfig(), Formatting.Indented));
                    isDefault = true;
                }
                else
                {
                    ConsoleUtils.WriteLine("Fix your config file and restart the application.", ConsoleColor.Red);
                    Console.Read();
                    Environment.Exit(0);
                }
            }
        }

        if (!File.Exists(APIConfigPath))
        {
            File.WriteAllText(APIConfigPath, JsonConvert.SerializeObject(new APIConfig(), Formatting.Indented));
        }
        else
        {
            try
            {
                var deserializedConfig = JsonConvert.DeserializeObject<APIConfig>(File.ReadAllText(APIConfigPath));
                APIConfig = deserializedConfig ?? throw new ArgumentException("File is empty.");
            }
            catch (Exception e)
            {
                ConsoleUtils.WriteLine("There is a problem with your api config: " + e);
                Console.Read();
                Environment.Exit(0);
            }
        }

        return isDefault;
    }

    public static void InteractiveSetup()
    {
        ConsoleUtils.WriteLine("Config setup", ConsoleColor.DarkCyan);
        ConsoleUtils.WriteLine("Press enter to use default values.", ConsoleColor.Yellow);
        var host = ConsoleUtils.ReadLine("Host (default: localhost): ", ConsoleColor.Cyan);
        var port = ConsoleUtils.ReadLine("Port (default: 3306): ", ConsoleColor.Cyan);
        var database = ConsoleUtils.ReadLine("Database (default: dbapi): ", ConsoleColor.Cyan);
        var username = ConsoleUtils.ReadLine("Username (default: root): ", ConsoleColor.Cyan);
        var password = ConsoleUtils.ReadLine("Password (default: none): ", ConsoleColor.Cyan);

        try
        {
            DBConfig = new DatabaseConfig()
            {
                Host = string.IsNullOrWhiteSpace(host) ? "localhost" : host,
                Port = string.IsNullOrWhiteSpace(port) ? (ushort)3306 : ushort.Parse(port),
                Database = string.IsNullOrWhiteSpace(database) ? "dbapi" : database,
                Username = string.IsNullOrWhiteSpace(username) ? "root" : username,
                Password = string.IsNullOrWhiteSpace(password) ? "" : password
            };
        }
        catch (FormatException e)
        {
            ConsoleUtils.WriteLine("Invalid format, using default.", ConsoleColor.Red);
            DBConfig = new DatabaseConfig();
            if(ConsoleUtils.GetUserConfirmation("Rerun? [Y/N]", ConsoleColor.Cyan))
                InteractiveSetup();
        }
    }
}