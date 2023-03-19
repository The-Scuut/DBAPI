namespace DBAPI.Models;

public struct Token
{
    public int Id;
    public Guid TokenGuid;
    public string? Name;
    public string? Description;
    public DateTime Created;

    public override string ToString() => $"Token #{Id} {Name}: {TokenGuid} created at {Created}";
}