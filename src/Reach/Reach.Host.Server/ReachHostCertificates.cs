using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Novolis.Transports.Tailscale;

namespace Novolis.Reach.Host.Server;

internal static class ReachHostCertificates
{
    internal static X509Certificate2 CreateQuicCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName("CN=Novolis Reach"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(30));
        var password = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));
        var pfx = generated.Export(X509ContentType.Pfx, password);
        try
        {
            return X509CertificateLoader.LoadPkcs12(
                pfx,
                password,
                X509KeyStorageFlags.MachineKeySet
                    | X509KeyStorageFlags.PersistKeySet,
                null);
        }
        catch (CryptographicException)
        {
            return X509CertificateLoader.LoadPkcs12(
                pfx,
                password,
                X509KeyStorageFlags.UserKeySet
                    | X509KeyStorageFlags.PersistKeySet,
                null);
        }
    }

    internal static IReadOnlyList<IPAddress> GetReachableIPv4Addresses()
    {
        var addresses = new List<IPAddress>();
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
                continue;

            foreach (var address in networkInterface
                         .GetIPProperties()
                         .UnicastAddresses
                         .Select(static item => item.Address)
                         .Where(static item =>
                             item.AddressFamily == AddressFamily.InterNetwork
                             && !IPAddress.IsLoopback(item))
                         .Where(IsPrivateOrTailscaleIPv4))
            {
                if (!addresses.Contains(address))
                    addresses.Add(address);
            }
        }

        return addresses;
    }

    private static bool IsPrivateOrTailscaleIPv4(IPAddress address)
    {
        if (TailscaleAddressEnumerator.IsTailscaleIPv4(address))
            return true;

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254);
    }
}
