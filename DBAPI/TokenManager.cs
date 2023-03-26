namespace DBAPI;

using System.Globalization;
using DBAPI.Models;

public static class TokenManager
{
    public static Guid SessionToken { get; } = Guid.NewGuid();
    public static async Task<Token[]> GetTokens()
    {
        int i = 0;
        List<Token> tokens = new();
        List<string> stringBuffer = new List<string>();
        await foreach (var tokenData in DBHandler.ExecuteQuery("SELECT * FROM tokens", null, true))
        {
            stringBuffer.Add(tokenData.ToString());
            if (i > 3)
            {
                tokens.Add(CreateTokenStruct(stringBuffer.ToArray()));
                stringBuffer.Clear();
                i = 0;
                continue;
            }
            i++;
        }
        return tokens.ToArray();
    }

    public static async Task<Token[]> GetTokensByName(string name)
    {
        int i = 0;
        List<Token> tokens = new();
        List<string> stringBuffer = new List<string>();
        await foreach (var tokenData in DBHandler.ExecuteQuery("SELECT * FROM tokens where name = @name", 
                           collection => collection.AddWithValue("name", name), true))
        {
            stringBuffer.Add(tokenData.ToString());
            if (i > 3)
            {
                tokens.Add(CreateTokenStruct(stringBuffer.ToArray()));
                stringBuffer.Clear();
                i = 0;
                continue;
            }
            i++;
        }
        return tokens.ToArray();
    }

    public static async Task<Token> CreateToken(string name, string token, string? description)
    {
        await DBHandler.ExecuteQuerySingle("INSERT INTO tokens(token, name, description) VALUES (@token, @name, @description)",
            collection =>
            {
                collection.AddWithValue("token", token);
                collection.AddWithValue("name", name);
                collection.AddWithValue("description", description ?? "");
            }, true);
        var result = DBHandler.ExecuteQuery("SELECT * FROM tokens WHERE token = @token",
            collection => { collection.AddWithValue("token", token); }, true);
        List<string> buffer = new List<string>();
        await foreach (var stringValue in result)
        {
            buffer.Add(stringValue.ToString());
        }
        return CreateTokenStruct(buffer.ToArray());
    }

    public static async Task RemoveToken(Token token)
    {
        await DBHandler.ExecuteQuerySingle("DELETE FROM tokens WHERE id = @id", collection => { collection.AddWithValue("id", token.Id); }, true);
    }

    public static async Task<Token?> GetToken(int id)
    {
        var result = DBHandler.ExecuteQuery("SELECT * FROM tokens WHERE id = @id",
            collection => { collection.AddWithValue("id", id); }, true);
        List<string> buffer = new List<string>();
        await foreach (var stringValue in result)
        {
            buffer.Add(stringValue.ToString());
        }
        if (buffer.Count == 0)
            return null;
        return CreateTokenStruct(buffer.ToArray());
    }

    public static async Task<Token?> GetToken(Guid id)
    {
        var result = DBHandler.ExecuteQuery("SELECT * FROM tokens WHERE token = @token",
            collection => { collection.AddWithValue("token", id.ToString("N")); }, true);
        List<string> buffer = new List<string>();
        await foreach (var stringValue in result)
        {
            buffer.Add(stringValue.ToString());
        }
        if (buffer.Count == 0)
            return null;
        return CreateTokenStruct(buffer.ToArray());
    }

    public static Token CreateTokenStruct(string[] queryResult) => new Token()
        {
            Id = Int32.Parse(queryResult[0]),
            TokenGuid = Guid.ParseExact(queryResult[1].ToCharArray(), "N"),
            Name = queryResult[2],
            Description = queryResult[3],
            Created = DateTime.ParseExact(queryResult[4], "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)
        };

    public static async Task<bool> IsValid(string token) => 
        token.TrimEnd('\0') == SessionToken.ToString() ||
               (Guid.TryParse(token, out var parsedGuid) &&
                await DBHandler.ExecuteQuerySingle("SELECT * FROM tokens WHERE token = @token",
                    collection => { collection.AddWithValue("token", parsedGuid.ToString("N")); }, true) != null);
    
}