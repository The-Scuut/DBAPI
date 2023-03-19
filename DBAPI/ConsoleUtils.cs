namespace DBAPI;

using DBAPI.Models;

public static class ConsoleUtils
{
    public static void Write(object input) => Console.Write(input.ToString() ?? "null");
    public static void Write(string input, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write(input);
        Console.ResetColor();
    }
    public static void Write(object input, ConsoleColor color) => Console.WriteLine(input.ToString() ?? "null", color);

    public static void WriteLine(object input) => Console.WriteLine(input.ToString() ?? "null");
    public static void WriteLine(string input, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(input);
        Console.ResetColor();
    }
    public static void WriteLine(object input, ConsoleColor color) => WriteLine(input.ToString() ?? "null", color);

    public static string? ReadLine(string prompt, ConsoleColor? color = null)
    {
        if (color != null)
        {
            Console.ForegroundColor = color.Value;
            Console.Write(prompt);
            Console.ResetColor();
            return Console.ReadLine();
        }
        Console.Write(prompt);
        return Console.ReadLine();
    }

    public static bool GetUserConfirmation(string prompt, ConsoleColor? color = null, bool? defaultResponse = null)
    {
        bool yes = false;
        bool no = false;

        while (!yes && !no)
        {
            var input = ReadLine(prompt, color)?.ToLower();
            if (input != null)
            {
                yes = Constants.Yes.Contains(input);
                no = Constants.No.Contains(input);
            }
            if (!yes && !no)
            {
                if (defaultResponse != null)
                {
                    return defaultResponse.Value;
                }
                WriteLine("Try again.");
                continue;
            }
            else
            {
                return yes;
            }
        }

        throw new IndexOutOfRangeException("This should never happen.");
    }

    public static void HandleCommand(string command, string[] args, out bool known)
    {
        known = true;
        switch (command.ToLower())
        {
            case "token":
                if (args.Length == 0)
                {
                    WriteLine("Usage: token <list|add|remove|get>");
                    break;
                }
                switch (args.ElementAt(0).ToLower())
                {
                    case "list":
                        var tokens = TokenManager.GetTokens().GetAwaiter().GetResult();
                        if(tokens.Length < 10)
                        {
                            WriteLine(string.Join(Environment.NewLine,
                                    tokens.Select(x => $"{x.Id} - {x.Name} - {x.Description} - {x.TokenGuid} - {x.Created}")),
                                ConsoleColor.Cyan);
                        }
                        else
                        {
                            WriteLine("Hiding descriptions, use 'token get <id>' to view them.", ConsoleColor.Yellow);
                            WriteLine(string.Join(Environment.NewLine,
                                    tokens.Select(x => $"{x.Id} - {x.Name} - {x.TokenGuid} - {x.Created}")),
                                ConsoleColor.Cyan);
                        }
                        break;
                    case "add":
                        var token = Guid.NewGuid().ToString("N");
                        string name = args.Length > 1 ? args.ElementAt(1) : ReadLine("Token Name (default: none): ", ConsoleColor.Cyan) ?? "";
                        string description = args.Length > 2 ? string.Join(' ', args.Skip(2)) : ReadLine("Token Description (default: none): ", ConsoleColor.Cyan) ?? "";
                        var created = TokenManager.CreateToken(name, token, description).GetAwaiter().GetResult();
                        if (created.Id == 0)
                        {
                            WriteLine("Failed to create token.", ConsoleColor.Red);
                            break;
                        }
                        WriteLine("Created " + created.ToString(), ConsoleColor.Green);
                        break;
                    case "remove":
                        string tokenId = args.Length > 1 ? args.ElementAt(1) : ReadLine("Token identifier (number or token): ", ConsoleColor.Cyan) ?? "";
                        if (int.TryParse(tokenId, out int id))
                        {
                            var tokenToDelete = TokenManager.GetToken(id).GetAwaiter().GetResult();
                            if (tokenToDelete == null)
                            {
                                WriteLine("Token not found.", ConsoleColor.Red);
                                break;
                            }
                            TokenManager.RemoveToken(tokenToDelete.Value).GetAwaiter().GetResult();
                            WriteLine("Deleted " + tokenToDelete.ToString(), ConsoleColor.Green);
                        }
                        else if (Guid.TryParse(tokenId, out var guid) || Guid.TryParseExact(tokenId, "N", out guid))
                        {
                            var tokenToDelete = TokenManager.GetToken(guid).GetAwaiter().GetResult();
                            if (tokenToDelete == null)
                            {
                                WriteLine("Token not found.", ConsoleColor.Red);
                                break;
                            }
                            TokenManager.RemoveToken(tokenToDelete.Value).GetAwaiter().GetResult();
                            WriteLine("Deleted " + tokenToDelete.ToString(), ConsoleColor.Green);
                        }
                        else
                        {
                            WriteLine("Invalid format: "+tokenId, ConsoleColor.Red);
                        }
                        break;
                    case "get":
                        if (args.Length < 3)
                        {
                            WriteLine("Usage: token get <id|token|name> <string>", ConsoleColor.Yellow);
                            break;
                        }
                        var type = args.ElementAt(1).ToLower();
                        var value = string.Join(' ', args.Skip(2));
                        Token? tokenGotten;
                        switch (type)
                        {
                            case "id":
                                if(int.TryParse(value, out int toGetId))
                                {
                                    tokenGotten = TokenManager.GetToken(toGetId).GetAwaiter().GetResult();
                                    if (tokenGotten == null)
                                    {
                                        WriteLine("Token not found.", ConsoleColor.Red);
                                        break;
                                    }
                                    WriteLine(tokenGotten.ToString(), ConsoleColor.Cyan);
                                    WriteLine("Description: " + tokenGotten.Value.Description, ConsoleColor.Cyan);
                                }
                                else
                                {
                                    WriteLine("Invalid int: " + value, ConsoleColor.Red);
                                }
                                break;
                            case "guid":
                            case "token":
                                if (Guid.TryParse(value, out var guid) || Guid.TryParseExact(value, "N", out guid))
                                {
                                    tokenGotten = TokenManager.GetToken(guid).GetAwaiter().GetResult();
                                    if (tokenGotten == null)
                                    {
                                        WriteLine("Token not found.", ConsoleColor.Red);
                                        break;
                                    }
                                    WriteLine(tokenGotten.ToString(), ConsoleColor.Cyan);
                                    WriteLine("Description: " + tokenGotten.Value.Description, ConsoleColor.Cyan);
                                }
                                else
                                {
                                    WriteLine("Invalid guid: " + value, ConsoleColor.Red);
                                }
                                break;
                            case "name":
                                value = value.Replace("$BLANKwhitespace", "");
                                var tokensByName = TokenManager.GetTokensByName(value).GetAwaiter().GetResult();
                                WriteLine("Tokens with name: " + value, ConsoleColor.DarkCyan);
                                if (tokensByName.Length == 0)
                                    WriteLine("None found.", ConsoleColor.Yellow);
                                WriteLine(string.Join(Environment.NewLine,
                                    tokensByName.Select(x =>
                                        $"{x.ToString()}" + Environment.NewLine + $"Description: {x.Description}")), ConsoleColor.Cyan);
                                break;
                            default:
                                WriteLine("Unknown type: " + type, ConsoleColor.Red);
                                break;
                        }
                        break;
                    default:
                        WriteLine("Unknown subcommand: " + args.ElementAt(0), ConsoleColor.Red);
                        break;
                }
                break;
            case "query":
                WriteLine("DO NOT USE THIS COMMAND UNLESS YOU KNOW WHAT YOU ARE DOING.", ConsoleColor.Red);
                WriteLine("DO NOT USE THIS COMMAND UNLESS YOU KNOW WHAT YOU ARE DOING.", ConsoleColor.Red);
                bool internaldb = false;
                if (args.Any(x => x == "-internal"))
                {
                    internaldb = true;
                    args = args.Where(x => x != "-internal").ToArray();
                }
                var query = string.Join(' ', args);
                if (query == "")
                {
                    WriteLine("Usage: query <query>");
                    break;
                }
                var result = DBHandler.ExecuteQuerySingle(query, null, internaldb).GetAwaiter().GetResult();
                WriteLine(result ?? "null", ConsoleColor.Cyan);
                break;
            case "fetch":
                string url = "";
                if (args.Length > 1 && args.ElementAt(0) == "v1")
                {
                    url = "/Application/V1/"+args.ElementAt(1);
                }
                else
                    url = args.Length > 0 ? args.ElementAt(0) : ReadLine("Location: ", ConsoleColor.Cyan) ?? "/";
                var response = ClientEmulator.GetAsync(url).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    WriteLine("Status: " + response.StatusCode, ConsoleColor.Green);
                    WriteLine("Content: " + response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), ConsoleColor.Cyan);
                }
                else
                {
                    WriteLine("Status: " + response.StatusCode, ConsoleColor.Red);
                    WriteLine("Content: " + response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), ConsoleColor.Red);
                }
                break;
            case "post":
                string urlPost = "";
                if (args.Length > 1 && args.ElementAt(0) == "v1")
                {
                    urlPost = "/Application/V1/"+args.ElementAt(1);
                }
                else
                    urlPost = args.Length > 0 ? args.ElementAt(0) : ReadLine("Location: ", ConsoleColor.Cyan) ?? "/";
                var responsePost = ClientEmulator.PostAsync(urlPost, string.Join(' ', args.Skip(2))).GetAwaiter().GetResult();
                if (responsePost.IsSuccessStatusCode)
                {
                    WriteLine("Status: " + responsePost.StatusCode, ConsoleColor.Green);
                    WriteLine("Content: " + responsePost.Content.ReadAsStringAsync().GetAwaiter().GetResult(), ConsoleColor.Cyan);
                }
                else
                {
                    WriteLine("Status: " + responsePost.StatusCode, ConsoleColor.Red);
                    WriteLine("Content: " + responsePost.Content.ReadAsStringAsync().GetAwaiter().GetResult(), ConsoleColor.Red);
                }
                break;
            case "s":
            case "shortcut":
                if (args.Length > 0 && Constants.Shortcuts.TryGetValue(args.ElementAt(0), out var shortcut))
                {
                    string[] formatted = shortcut.Split(' ');
                    HandleCommand(formatted[0], formatted.Skip(1).ToArray(), out _);
                }
                else
                {
                    WriteLine("Unknown shortcut: " + args.ElementAt(0), ConsoleColor.Red);
                }
                break;
            case "exit":
            case "quit":
            case "q":
                Environment.Exit(0);
                break;
            default:
                known = false;
                break;
        }
    }
}