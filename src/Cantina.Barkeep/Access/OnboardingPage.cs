// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Text;
using Cantina.Barkeep.Network;

namespace Cantina.Barkeep.Access;

/// <summary>
/// The one page Barkeep serves over plain HTTP on the LAN.
///
/// It has two shapes, and which one it takes is the clearest signal of how the theater is
/// configured (D-029):
///
/// <list type="bullet">
/// <item>With a **supplied, publicly trusted certificate**, there is nothing to install and
/// the page says so in a sentence. It exists only to send the browser to the secure
/// address.</item>
/// <item>With the **private theater authority**, it hands over the certificate and walks
/// through trusting it, with the fingerprint to compare. It is hand-written rather than part
/// of the client bundle because it has to work before the device trusts the theater, which
/// is exactly when the client cannot load.</item>
/// </list>
///
/// Either way it grants nothing, so serving it unauthenticated over an unencrypted transport
/// costs nothing — a certificate is public by construction, and the fingerprint printed
/// beside it is what makes tampering visible.
/// </summary>
public static class OnboardingPage
{
    public static string Render(TheaterEndpoints endpoints, TheaterCertificates? certificates)
    {
        var address = endpoints.LanAddress?.ToString() ?? IPAddress.Loopback.ToString();
        var host = endpoints.HostNames.FirstOrDefault(name =>
            name.Contains('.', StringComparison.Ordinal) && !name.EndsWith(".local", StringComparison.Ordinal))
            ?? address;
        var secure = endpoints.SecurePort == 443
            ? $"https://{host}"
            : $"https://{host}:{endpoints.SecurePort}";

        var page = new StringBuilder();
        Head(page);

        if (certificates?.NeedsDeviceTrust is true)
        {
            TrustSteps(page, certificates, secure);
        }
        else
        {
            NothingToInstall(page, secure);
        }

        page.Append("</main></body></html>");
        return page.ToString();
    }

    private static void Head(StringBuilder page)
    {
        page.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        page.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        page.Append("<title>Pair this iPad with Cantina</title><style>");
        page.Append("body{font:17px/1.5 -apple-system,system-ui,sans-serif;margin:0;padding:2rem;");
        page.Append("background:#14110f;color:#f2ede6}main{max-width:34rem;margin:0 auto}");
        page.Append("h1{font-size:1.6rem;margin:0 0 1rem}ol{padding-left:1.2rem}li{margin:.6rem 0}");
        page.Append("code,.fp{font-family:ui-monospace,Menlo,monospace;font-size:.9rem;word-break:break-all}");
        page.Append(".fp{display:block;background:#241f1b;padding:.75rem;border-radius:.5rem;margin:.5rem 0}");
        page.Append("a.button{display:inline-block;background:#c4602a;color:#fff;text-decoration:none;");
        page.Append("padding:.7rem 1.1rem;border-radius:.5rem;margin:.5rem 0}");
        page.Append("</style></head><body><main>");
    }

    private static void NothingToInstall(StringBuilder page, string secure)
    {
        page.Append("<h1>Nothing to install.</h1>");
        page.Append("<p>This theater serves a publicly trusted certificate, so this iPad already ");
        page.Append("trusts it. There is no profile to install and no fingerprint to compare.</p>");
        page.Append("<p><a class=\"button\" href=\"");
        page.Append(Escape(secure));
        page.Append("\">Open Cantina</a></p>");
        page.Append("<p>Enter the pairing code shown on the theater PC, then Share &rsaquo; ");
        page.Append("<strong>Add to Home Screen</strong>.</p>");
        page.Append("<p>The pairing code is never shown on this page. It appears only on the theater PC, ");
        page.Append("so being in the room is what authorises a new device.</p>");
    }

    private static void TrustSteps(StringBuilder page, TheaterCertificates certificates, string secure)
    {
        page.Append("<h1>Pair this iPad with Cantina</h1>");
        page.Append("<p>This page is served without encryption on purpose: it exists to give this iPad ");
        page.Append("the certificate that makes encryption possible. It grants no control over the theater.</p>");
        page.Append("<ol>");
        page.Append("<li><a class=\"button\" href=\"/");
        page.Append(Escape(TheaterCertificateAuthority.AuthorityPublicFileName));
        page.Append("\">Download the theater certificate</a><br>Safari will offer to review a profile. Allow it.</li>");
        page.Append("<li>Open <strong>Settings &rsaquo; General &rsaquo; VPN &amp; Device Management</strong> ");
        page.Append("and install the <em>Cantina Theater CA</em> profile.</li>");

        if (certificates.AuthorityFingerprint is { } fingerprint)
        {
            page.Append("<li>Before you tap Install, check the fingerprint matches the one on the theater PC:");
            page.Append("<span class=\"fp\">");
            page.Append(Escape(fingerprint));
            page.Append("</span></li>");
        }

        page.Append("<li>Open <strong>Settings &rsaquo; General &rsaquo; About &rsaquo; Certificate Trust Settings</strong> ");
        page.Append("and switch on full trust for <em>Cantina Theater CA</em>.</li>");
        page.Append("<li>Go to <a href=\"");
        page.Append(Escape(secure));
        page.Append("\">");
        page.Append(Escape(secure));
        page.Append("</a> and enter the pairing code shown on the theater PC.</li>");
        page.Append("<li>Share &rsaquo; <strong>Add to Home Screen</strong>.</li>");
        page.Append("</ol>");
        page.Append("<p>The pairing code is never shown on this page. It appears only on the theater PC, ");
        page.Append("so being in the room is what authorises a new device.</p>");
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
