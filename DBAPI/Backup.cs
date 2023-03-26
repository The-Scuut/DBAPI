namespace DBAPI;

using MySqlConnector;

public static class Backup
{
    public static void BackupDatabase(string backupPath)
    {
        using var connection = new MySqlConnection(ConfigManager.DBConfig.GetConnectionString());
        using MySqlCommand cmd = new MySqlCommand();
        using MySqlBackup mb = new MySqlBackup(cmd);
        cmd.Connection = connection;
        connection.Open();
        mb.ExportToFile(backupPath);
        connection.Close();
    }

    public static void RestoreDatabase(string backupPath)
    {
        using var connection = new MySqlConnection(ConfigManager.DBConfig.GetConnectionString());
        using MySqlCommand cmd = new MySqlCommand();
        using MySqlBackup mb = new MySqlBackup(cmd);
        cmd.Connection = connection;
        connection.Open();
        mb.ImportFromFile(backupPath);
        connection.Close();
    }
}