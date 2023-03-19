namespace DBAPI.Modules;

using System.IO.Compression;
using System.Reflection;

public static class ModuleLoader
{
    public static Assembly[] Modules;
    public static Assembly[] Dependencies;
    private static List<Assembly> _modules = new();
    private static List<Assembly> _dependencies = new();

    public static void LoadModules()
    {
        var dependencyPath = Path.Combine(ConfigManager.APIConfig.ModulePath, "dependencies");
        Directory.CreateDirectory(dependencyPath);
        foreach (var dependency in Directory.GetFiles(dependencyPath, "*.dll"))
        {
            try
            {
                var assembly = Assembly.Load(File.ReadAllBytes(dependency));
                GetEmbeddedDependencies(assembly);
                _dependencies.Add(assembly);
            }
            catch (Exception e)
            {
                ConsoleUtils.WriteLine("Error while loading dependencies: " + e, ConsoleColor.Red);
            }
        }
        Dependencies = _dependencies.ToArray();
        ConsoleUtils.WriteLine("Loaded " + Dependencies.Length + " dependencies", ConsoleColor.Green);

        Directory.CreateDirectory(ConfigManager.APIConfig.ModulePath);
        foreach (var module in Directory.GetFiles(ConfigManager.APIConfig.ModulePath, "*.dll"))
        {
            try
            {
                var assembly = Assembly.Load(File.ReadAllBytes(module));
                GetEmbeddedDependencies(assembly);
                _modules.Add(assembly);
            }
            catch (Exception e)
            {
                ConsoleUtils.WriteLine("Error while loading module: " + e, ConsoleColor.Red);
            }
        }
        Modules = _modules.ToArray();
        ConsoleUtils.WriteLine("Loaded " + Modules.Length + " modules", ConsoleColor.Green);
    }

    public static void EnableModules()
    {
        int enabled = 0;
        foreach (var module in Modules)
        {
            foreach (var type in module.GetTypes())
            {
                if (!type.IsAssignableFrom(typeof(ModuleBase)))
                    continue;
                try
                {
                    var instance = (ModuleBase)Activator.CreateInstance(type);
                    if (instance == null)
                    {
                        ConsoleUtils.WriteLine("Cannot create instance of module " + module.GetName().Name + " " + type.Name, ConsoleColor.Red);
                        continue;
                    }
                    instance.Enable();
                    enabled++;
                }
                catch (Exception e)
                {
                    ConsoleUtils.WriteLine($"Error loading module {module.GetName().Name} {type.Name}: " + e, ConsoleColor.Red);
                }
            }
        }
        ConsoleUtils.WriteLine("Enabled " + enabled + " modules", ConsoleColor.Green);
    }

    private static void GetEmbeddedDependencies(Assembly assembly)
    {
        foreach (var dependency in assembly.GetManifestResourceNames())
        {
            if (dependency.EndsWith(".dll"))
            {
                using (var stream = assembly.GetManifestResourceStream(dependency))
                {
                    if (stream == null)
                        continue;
                    using MemoryStream memoryStream = new();
                    stream.CopyTo(memoryStream);
                    try
                    {
                        _dependencies.Add(Assembly.Load(memoryStream.ToArray()));
                    }
                    catch (Exception e)
                    {
                        ConsoleUtils.WriteLine("Error while loading embedded dependency: " + e, ConsoleColor.Red);
                    }
                }
            }
            else if (dependency.EndsWith(".dll.compressed"))
            {
                using (var stream = assembly.GetManifestResourceStream(dependency))
                {
                    if (stream == null)
                        continue;
                    using DeflateStream deflateStream = new(stream, CompressionMode.Decompress);
                    using MemoryStream memoryStream = new();
                    deflateStream.CopyTo(memoryStream);
                    try
                    {
                        _dependencies.Add(Assembly.Load(memoryStream.ToArray()));
                    }
                    catch (Exception e)
                    {
                        ConsoleUtils.WriteLine("Error while loading embedded compressed dependency: " + e, ConsoleColor.Red);
                    }
                }
            }
        }
    }
}