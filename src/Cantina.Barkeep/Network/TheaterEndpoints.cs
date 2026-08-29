// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Cantina.Barkeep.Network;

/// <summary>
/// The resolved answer to "where does Barkeep listen, and who is allowed to say so".
///
/// Everything downstream — Kestrel's listeners, the certificate's subject names, host
/// filtering, the origin check, and the firewall rule printed for the operator — is derived
/// from this one record, so the certificate cannot name an address the server does not bind
/// and the origin check cannot drift from the port it protects.
/// </summary>
public sealed record TheaterEndpoints(
    BarkeepBinding Mode,
    IPAddress? LanAddress,
    int LanPrefixLength,
    int Port,
    int SecurePort,
    IReadOnlyList<string> HostNames,
    IReadOnlyList<string> AllowedHosts,
    IReadOnlyList<string> AllowedOrigins)
{
    /// <summary>The addresses the certificate must cover. Loopback is included so one certificate serves both listeners.</summary>
    public IReadOnlyList<IPAddress> CertificateAddresses =>
        LanAddress is null ? [IPAddress.Loopback] : [IPAddress.Loopback, LanAddress];

    /// <summary>
    /// Resolve from configuration. The address is chosen explicitly or from the interface
    /// holding the default IPv4 gateway; tunnels and loopbacks are never candidates.
    /// </summary>
    public static TheaterEndpoints Resolve(NetworkOptions options, string machineName, bool development)
    {
        var lan = options.Mode == BarkeepBinding.Lan
            ? SelectAddress(options.Address)
            : (Address: (IPAddress?)null, PrefixLength: 0);
        var names = new List<string> { "localhost" };
        var lowerMachine = machineName.ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(lowerMachine))
        {
            names.Add(lowerMachine);
            names.Add($"{lowerMachine}.local");
        }

        foreach (var name in options.HostNames)
        {
            var trimmed = name.Trim().ToLowerInvariant();
            if (trimmed.Length > 0 && !names.Contains(trimmed, StringComparer.Ordinal))
            {
                names.Add(trimmed);
            }
        }

        var hosts = new List<string>(names) { "127.0.0.1", "[::1]" };

        if (lan.Address is not null)
        {
            hosts.Add(lan.Address.ToString());
        }

        var origins = new List<string>();

        foreach (var host in hosts)
        {
            origins.Add($"http://{host}:{options.Port}");
            origins.Add($"https://{host}:{options.SecurePort}");
        }

        foreach (var origin in options.AdditionalOrigins)
        {
            var trimmed = origin.Trim().TrimEnd('/');
            if (trimmed.Length > 0 && !origins.Contains(trimmed, StringComparer.Ordinal))
            {
                origins.Add(trimmed);
            }
        }

        // The Vite dev server is a different origin from Barkeep and is proxied, not
        // hosted. It is allowed while developing and never in a shipped configuration.
        if (development)
        {
            origins.Add("http://localhost:5173");
            origins.Add("http://127.0.0.1:5173");
        }

        return new TheaterEndpoints(
            options.Mode,
            lan.Address,
            lan.PrefixLength,
            options.Port,
            options.SecurePort,
            names,
            hosts,
            origins);
    }

    /// <summary>
    /// The least-scope inbound rule this binding needs: two TCP ports, the private profile,
    /// this program, and the local subnet only. Barkeep prints it and never runs it —
    /// changing the firewall is the operator's decision (D-026).
    /// </summary>
    public string FirewallCommand(string programPath) =>
        $"""netsh advfirewall firewall add rule name="Cantina Barkeep" dir=in action=allow protocol=TCP localport={Port},{SecurePort} profile=private remoteip={Subnet()} program="{programPath}" enable=yes""";

    public static string FirewallRemovalCommand() =>
        "netsh advfirewall firewall delete rule name=\"Cantina Barkeep\"";

    /// <summary>The CIDR of the bound interface, so the rule admits the theater's own subnet and nothing else.</summary>
    public string Subnet()
    {
        if (LanAddress is null || LanPrefixLength <= 0)
        {
            return "LocalSubnet";
        }

        var bytes = LanAddress.GetAddressBytes();
        var mask = LanPrefixLength;

        for (var index = 0; index < bytes.Length; index++)
        {
            var bits = Math.Clamp(mask - (index * 8), 0, 8);
            bytes[index] &= (byte)(bits == 0 ? 0 : 0xFF << (8 - bits));
        }

        return $"{new IPAddress(bytes)}/{LanPrefixLength}";
    }

    private static (IPAddress? Address, int PrefixLength) SelectAddress(string configured)
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();

        if (!string.IsNullOrWhiteSpace(configured))
        {
            var wanted = IPAddress.Parse(configured.Trim());
            foreach (var candidate in interfaces)
            {
                foreach (var unicast in candidate.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.Equals(wanted))
                    {
                        return (wanted, unicast.PrefixLength);
                    }
                }
            }

            // Configured but not present: bind it anyway and let the bind failure be the
            // report. Silently choosing a different address is how a server ends up
            // listening somewhere nobody asked for.
            return (wanted, 24);
        }

        foreach (var candidate in interfaces)
        {
            if (candidate.OperationalStatus != OperationalStatus.Up ||
                candidate.NetworkInterfaceType is NetworkInterfaceType.Loopback
                    or NetworkInterfaceType.Tunnel
                    or NetworkInterfaceType.Ppp)
            {
                continue;
            }

            var properties = candidate.GetIPProperties();
            var routed = properties.GatewayAddresses.Any(gateway =>
                gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                !gateway.Address.Equals(IPAddress.Any));

            if (!routed)
            {
                continue;
            }

            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(unicast.Address))
                {
                    return (unicast.Address, unicast.PrefixLength);
                }
            }
        }

        return (null, 0);
    }
}
