namespace DBAPI;

using System.Security.Cryptography.X509Certificates;
using Certes;
using Certes.Acme;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

public class CertificateManager
{
    public string Path { get; }
    public string? Password { get; private set; }
    public bool IsSelfSigned { get; private set; }

    public bool Exists => File.Exists(Path);
    public bool TryGetCertificate(out X509Certificate2? certificate)
    {
        if (Exists)
        {
            try
            {
                certificate = new X509Certificate2(Path, Password);
                IsSelfSigned = certificate.Issuer == certificate.Subject;
                if (IsSelfSigned)
                {
                    ConsoleUtils.WriteLine("Using self signed certificate.", ConsoleColor.Gray);
                }
                else if (!certificate.Verify())
                    throw new Exception("Certificate is not valid.");
                return true;
            }
            catch (Exception e)
            {
                ConsoleUtils.WriteLine("There was is problem with your certificate: " + e, ConsoleColor.Red);
                certificate = null;
                return false;
            }
        }
        else
        {
            certificate = null;
            return false;
        }
    }

    public bool TryCreateSelfSigned(string ip)
    {
        try
        {
            var secureRandom = new SecureRandom();
            var generator = new X509V3CertificateGenerator();
            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(0x10001), secureRandom, 1024, 100));
            var keyPair = keyPairGenerator.GenerateKeyPair();
            var dn = new X509Name($"CN={ip}");

            generator.SetSerialNumber(BigInteger.ValueOf(DateTime.UtcNow.Ticks));
            generator.SetSubjectDN(dn);
            generator.SetIssuerDN(dn);
            generator.SetNotBefore(DateTime.Today.Subtract(TimeSpan.FromDays(1)));
            generator.SetNotAfter(DateTime.Today.AddYears(2));
            generator.SetPublicKey(keyPair.Public);

            var cert = generator.Generate(new Asn1SignatureFactory("SHA256WithRSAEncryption", keyPair.Private));
            using var stream = new FileStream(Path, FileMode.OpenOrCreate, FileAccess.Write);
            Pkcs12Store p12 = new Pkcs12Store();
            p12.SetKeyEntry(ip, new AsymmetricKeyEntry(keyPair.Private), new X509CertificateEntry[] { new (cert) });
            Password ??= "";
            p12.Save(stream, Password.ToCharArray(), secureRandom);
            stream.Dispose();
        }
        catch (Exception e)
        {
            ConsoleUtils.WriteLine("There was a problem with generating your certificate: " + e, ConsoleColor.Red);
            return false;
        }

        return true;
    }

    public bool TryRequestCertificate(Action<string, string > runApp)
    {
        var acme = new AcmeContext(
#if DEBUG
            WellKnownServers.LetsEncryptStagingV2
#else
            WellKnownServers.LetsEncryptV2
#endif
        );
        var email = ConsoleUtils.ReadLine("Enter your email address:", ConsoleColor.Cyan);
        bool agree = ConsoleUtils.GetUserConfirmation($"Do you agree to the terms of service ({acme.TermsOfService().GetAwaiter().GetResult()})? [Y/N]", ConsoleColor.Yellow, false);
        if (!agree)
        {
            ConsoleUtils.WriteLine("You must agree to the terms of service to continue.", ConsoleColor.Red);
            return false;
        }

        try
        {
            var account = acme.NewAccount(email, agree).GetAwaiter().GetResult();
            if (account == null)
            {
                ConsoleUtils.WriteLine("Could not create your account.", ConsoleColor.Red);
                return false;
            }

            var domain = ConsoleUtils.ReadLine("Enter the domain that is pointing to this machine:", ConsoleColor.Cyan)?
                .ToLower()
                .Replace("https://", "")
                .Replace("http://", "");
            if (string.IsNullOrWhiteSpace(domain))
            {
                ConsoleUtils.WriteLine("You must enter a domain.", ConsoleColor.Red);
                return false;
            }
            var order = acme.NewOrder(new[] { domain }).GetAwaiter().GetResult();
            var authz = order.Authorization(domain).GetAwaiter().GetResult();
            var httpChallenge = authz.Http().GetAwaiter().GetResult();
            var keyAuth = httpChallenge.KeyAuthz;
            Task.Run(() => runApp("/.well-known/acme-challenge/"+httpChallenge.Token, keyAuth));
            var connTest = ClientEmulator.GetAsyncExtern("http://" + domain + "/.well-known/acme-challenge/" + httpChallenge.Token)
                .GetAwaiter().GetResult();
            if (!connTest.IsSuccessStatusCode)
            {
                ConsoleUtils.WriteLine($"Connection test failed ({connTest.StatusCode} {connTest.Content.ReadAsStringAsync().GetAwaiter().GetResult()}).", ConsoleColor.Red);
                return false;
            }
            if (connTest.Content.ReadAsStringAsync().GetAwaiter().GetResult() != keyAuth)
            {
                ConsoleUtils.WriteLine($"Connection test failed (content does not match, got {connTest.Content}, expected: {httpChallenge.Token}).", ConsoleColor.Red);
                return false;
            }
            ConsoleUtils.WriteLine("Waiting for challenge to be verified...", ConsoleColor.Gray);
            httpChallenge.Validate().GetAwaiter().GetResult();
            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            order.Generate(
                new CsrInfo
                {
                    CountryName = ConsoleUtils.ReadLine("Enter your country code:", ConsoleColor.Cyan) ?? "US",
                    State = ConsoleUtils.ReadLine("Enter your state:", ConsoleColor.Cyan) ?? "Ohio",
                    Locality = ConsoleUtils.ReadLine("Enter your city:", ConsoleColor.Cyan) ?? "Shitsville",
                    Organization = ConsoleUtils.ReadLine("Enter your organization:", ConsoleColor.Cyan) ?? "NA",
                    OrganizationUnit = ConsoleUtils.ReadLine("Enter your organization unit:", ConsoleColor.Cyan) ?? "NA",
                    CommonName = domain,
                }, privateKey).GetAwaiter().GetResult();
            var certChain = order.Download().GetAwaiter().GetResult();
            var pfxBuilder = certChain.ToPfx(privateKey);
            Password ??= "";
            var pfx = pfxBuilder.Build(domain+"-crt", Password);
            File.WriteAllBytes(Path, pfx);
        }
        catch(Exception e)
        {
            ConsoleUtils.WriteLine("There was an requesting your certificate: " + e, ConsoleColor.Red);
            return false;
        }

        return true;
    }

    public CertificateManager(string path, string? password = null)
    {
        Path = path;
        Password = password;
    }
}

public static class CurrentCertificateInfo
{
    public static CertificateManager? Manager { get; set; }
    public static X509Certificate2? Certificate { get; set; }
}