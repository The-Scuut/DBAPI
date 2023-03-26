namespace DBAPI;

public class Constants
{
    public static readonly string[] Yes = new string[] { "y", "yes", "true", "1" };
    public static readonly string[] No = new string[] { "n", "no", "false", "0" };
    public static readonly string[] RequiredPermissions = new string[] { "ALTER", "CREATE", "DELETE", "DROP", "INSERT", "RELOAD", "SELECT", "SHOW DATABASES", "UPDATE" };

    public static readonly Dictionary<string, string> Shortcuts = new Dictionary<string, string>()
    {
        ["ctable"] = "post v1 datastore/table/create/testname {\"types\" : \"exampleValue int\"}",
        ["selectall"] = "fetch v1 datastore/selectall/testname",
    };
}