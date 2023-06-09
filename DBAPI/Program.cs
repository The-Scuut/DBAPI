using System.Net;
using System.Security.Cryptography.X509Certificates;
using DBAPI;
using DBAPI.Controllers;
using DBAPI.Modules;
using DBAPI.Setup;

var isDefault = ConfigManager.EnsureConfigsExists();
if (isDefault)
{
    var doSetup = ConsoleUtils.GetUserConfirmation("Your config is the default config, perform setup? [Y/N]", ConsoleColor.Yellow, false);
    if (doSetup)
        ConfigManager.InteractiveSetup();
}
ConsoleUtils.WriteLine("Attempting connection to database...", ConsoleColor.Cyan);
bool dbSuccess = DBSetup.AttemptConnection();
if (!dbSuccess)
{
    ConsoleUtils.WriteLine("Database configuration wrong.", ConsoleColor.Red);
    Console.Read();
    Environment.Exit(0);
}

bool httpsValid = false;
X509Certificate2 cert = null;
var certManager = new CertificateManager(ConfigManager.APIConfig.CertificatePath, ConfigManager.APIConfig.CertificatePassword);
if (!certManager.Exists)
{
    var gen = ConsoleUtils.GetUserConfirmation("No valid certificate, do you want to request one? [Y/N]", ConsoleColor.Yellow, false);
    if (gen)
    {
        var selfSigned = !ConsoleUtils.GetUserConfirmation("Will there be a domain pointing to this server? [Y/N]", ConsoleColor.Yellow, false);
        if (selfSigned)
        {
            string ip = "";
#if DEBUG
            if (ConsoleUtils.GetUserConfirmation("Use localhost? [Y/N]", ConsoleColor.Yellow, true))
            {
                ip = "localhost";
                goto SkipIpCheck;
            }
#endif
            Sign:
            ip = "";
            try
            {
                var response = ClientEmulator.GetAsyncExtern("https://ifconfig.me/ip").GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                ip = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                ConsoleUtils.WriteLine("Could not get external IP: " + e, ConsoleColor.Red);
                var manual = ConsoleUtils.ReadLine("Enter the ip address pointing to this machine:", ConsoleColor.Yellow);
                if (string.IsNullOrWhiteSpace(manual) || !IPAddress.TryParse(manual, out var parsedAddress))
                {
                    ConsoleUtils.WriteLine("Invalid IP.", ConsoleColor.Red);
                    certManager = null;
                }
                else
                {
                    ip = parsedAddress.ToString();
                }
            }

            SkipIpCheck:
            if (certManager?.TryCreateSelfSigned(ip) ?? false)
            {
                ConsoleUtils.WriteLine("Created self-signed certificate.", ConsoleColor.Green);
            }
            else
            {
                ConsoleUtils.WriteLine("Generating self-signed certificate failed.", ConsoleColor.Red);
                if (ConsoleUtils.GetUserConfirmation("Retry? [Y/N]", ConsoleColor.Yellow, false))
                    goto Sign;
                certManager = null;
            }
        }
        else
        {
            var cancellationToken = new CancellationTokenSource();
            Request:
            if (certManager.TryRequestCertificate((key, value) => Run(true, true,
                    application => application.MapGet(key, () => value), cancellationToken.Token)))
            {
                ConsoleUtils.WriteLine("Certificate request successful.", ConsoleColor.Green);
                cancellationToken.Cancel();
            }
            else
            {
                ConsoleUtils.WriteLine("Certificate request failed.", ConsoleColor.Red);
                if (ConsoleUtils.GetUserConfirmation("Retry? [Y/N]", ConsoleColor.Yellow, false))
                    goto Request;
                certManager = null;
            }
        }
    }
    else
    {
        certManager = null;
    }

    if (certManager == null)
    {
        ConsoleUtils.WriteLine("SSL disabled.", ConsoleColor.Red);
    }
}

if (certManager?.TryGetCertificate(out cert!) ?? false)
{
    ConsoleUtils.WriteLine("Loaded certificate: "+cert.FriendlyName, ConsoleColor.Green);
    CurrentCertificateInfo.Manager = certManager;
    CurrentCertificateInfo.Certificate = cert;
    httpsValid = true;
}

ConsoleUtils.WriteLine("Loading modules...", ConsoleColor.Cyan);
ModuleLoader.LoadModules();
ModuleLoader.EnableModules();

Task.Run(() =>
{
    Task.Delay(1000).Wait();
    ConsoleUtils.WriteLine("Listening to commands...", ConsoleColor.Blue);
    while (true)
    {
        var input = ConsoleUtils.ReadLine(">", ConsoleColor.Green);
        if (input == null)
            break;
        string[] args = input.Split(' ');
        bool known = true;
        try
        {
            ConsoleUtils.HandleCommand(args[0], args.Skip(1).ToArray(), out known);
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine("Error: " + e, ConsoleColor.Red);
        }
        if (!known)
        {
            ConsoleUtils.WriteLine("Unknown command: " + args[0], ConsoleColor.Red);
        }
    }
});

ConsoleUtils.WriteLine("Starting webserver...", ConsoleColor.Blue);
Run();
void Run(bool silent = false, bool disableMappings = false, Action<WebApplication>? configure = null, CancellationToken cancellationToken = default)
{
    bool https = httpsValid && ConfigManager.APIConfig.PortHTTPS != 0;
    var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
    builder.Services.AddControllersWithViews()
        .AddNewtonsoftJson();
    builder.WebHost.UseKestrel(options =>
    {
        if (ConfigManager.APIConfig.Port != 0)
            options.Listen(IPAddress.Any, ConfigManager.APIConfig.Port);

        if (https)
        {
            options.Listen(IPAddress.Any, ConfigManager.APIConfig.PortHTTPS, listenOptions =>
            {
                listenOptions.UseHttps(cert);
            });
        }
    });

    if (silent)
    {
        builder.Logging.ClearProviders();
    }

    var app = builder.Build();

// Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        if (ConfigManager.APIConfig.UseHSTS && https)
        {
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
    }

    if (ConfigManager.APIConfig.HTTPSRedirect && https)
        app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    //app.UseAuthorization();

    if(!disableMappings)
    {
        app.UseMiddleware<AuthenticationMiddleware>();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
    }
    if (configure != null)
        configure(app);

    app.RunAsync(cancellationToken).GetAwaiter().GetResult();
}