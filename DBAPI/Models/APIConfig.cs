namespace DBAPI.Models;

using Newtonsoft.Json;

public class APIConfig
{
    public string CertificatePath { get; set; } = "certificate.pfx";
    public string? CertificatePassword { get; set; } = null;
    public ushort Port { get; set; } = 80;
    public ushort PortHTTPS { get; set; } = 443;
    public string ModulePath { get; set; } = "Modules";
    public bool UseHSTS { get; set; } = false;
    public bool HTTPSRedirect { get; set; } = false;
    public bool RequireTokenForLocalhost { get; set; } = true;

    public void Save()
    {
        File.WriteAllText(ConfigManager.APIConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
    }
}