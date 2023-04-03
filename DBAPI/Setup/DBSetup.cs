namespace DBAPI.Setup;

using MySqlConnector;

public static class DBSetup
{
    public static bool AttemptConnection()
    {
        try
        {
            using var connection = new MySqlConnection(ConfigManager.DBConfig.GetConnectionStringNoDatabase());
            connection.Open();
            using var showGrantsCommand = new MySqlCommand("SHOW GRANTS FOR CURRENT_USER", connection);
            using var reader = showGrantsCommand.ExecuteReader();
            string perms = "";
            while (reader.Read())
            {
                perms += reader.GetString(0);
            }
            if (!perms.Contains("ALL PRIVILEGES"))
            {

                var missing = Constants.RequiredPermissions.Where(x => !perms.Contains(x)).ToArray();
                if (missing.Any())
                {
                    ConsoleUtils.WriteLine("The user is missing the following permissions:", ConsoleColor.Red);
                    foreach (var perm in missing)
                    {
                        ConsoleUtils.WriteLine(perm, ConsoleColor.Red);
                    }

                    return false;
                }
            }
            reader.Dispose();
            showGrantsCommand.Dispose();
            using var createDatabaseInternalCommand = new MySqlCommand("CREATE DATABASE IF NOT EXISTS " + ConfigManager.DBConfig.DatabaseInternal, connection);
            createDatabaseInternalCommand.ExecuteNonQuery();
            createDatabaseInternalCommand.Dispose();
            using var createTokenTableCommand = new MySqlCommand($"CREATE TABLE IF NOT EXISTS {ConfigManager.DBConfig.DatabaseInternal}.tokens(id INT AUTO_INCREMENT PRIMARY KEY, token VARCHAR(32) NOT NULL, name VARCHAR(32) NOT NULL, description TEXT NOT NULL, created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP)", connection);
            using var createDatabaseCommand = new MySqlCommand("CREATE DATABASE IF NOT EXISTS " + ConfigManager.DBConfig.Database, connection);
            createDatabaseCommand.ExecuteNonQuery();
            createDatabaseCommand.Dispose();
            createTokenTableCommand.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine("There was a problem setting up the database connection: " + e, ConsoleColor.Red);
            return false;
        }

        ConsoleUtils.WriteLine("Connection successful.", ConsoleColor.Green);
        return true;
    }
}