namespace DBAPI;

using MySqlConnector;

public static class DBHandler
{
    public static async Task<object?> ExecuteQuerySingle(string query, Action<MySqlParameterCollection>? paramsconconfig = null, bool internalDb = false)
    {
        await using MySqlConnection connection = new(ConfigManager.DBConfig.GetConnectionString(internalDb));
        await connection.OpenAsync();
        await using MySqlCommand command = new(query, connection);
        if (paramsconconfig != null)
        {
            paramsconconfig(command.Parameters);
        }
        var result = await command.ExecuteScalarAsync();
        return result;
    }
    
    public static async IAsyncEnumerable<object> ExecuteQuery(string query, Action<MySqlParameterCollection>? paramsconconfig = null, bool internalDb = false)
    {
        await using MySqlConnection connection = new(ConfigManager.DBConfig.GetConnectionString(internalDb));
        await connection.OpenAsync();
        await using MySqlCommand command = new(query, connection);
        if (paramsconconfig != null)
        {
            paramsconconfig(command.Parameters);
        }
        await using MySqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            for (int i = 0; i < reader.FieldCount; i++)
                yield return reader.GetValue(i);
        }
    }

    public static async Task<object?> ExecuteNonQuery(string query, Action<MySqlParameterCollection>? paramsconconfig = null, bool internalDb = false)
    {
        await using MySqlConnection connection = new(ConfigManager.DBConfig.GetConnectionString(internalDb));
        await connection.OpenAsync();
        await using MySqlCommand command = new(query, connection);
        if (paramsconconfig != null)
        {
            paramsconconfig(command.Parameters);
        }
        var result = await command.ExecuteNonQueryAsync();
        return result;
    }

    public static async Task<bool> CheckTableExists(string tableName, bool internalDb = false)
    {
        await using MySqlConnection connection = new(ConfigManager.DBConfig.GetConnectionString(internalDb));
        await connection.OpenAsync();
        await using MySqlCommand command = new("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @database AND table_name = @table", connection);
        command.Parameters.AddWithValue("database",
            internalDb ? ConfigManager.DBConfig.DatabaseInternal : ConfigManager.DBConfig.Database);
        command.Parameters.AddWithValue("table", tableName);
        var result = await command.ExecuteScalarAsync();
        return result != null && (long) result > 0;
    }
}