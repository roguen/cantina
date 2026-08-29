// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using Cantina.Barkeep.Network;

namespace Cantina.Barkeep.Access;

/// <summary>
/// One place where every request is decided, because a control surface with two places is a
/// control surface with a gap.
///
/// The rules, in the order they apply:
///
/// <list type="number">
/// <item>An <c>Origin</c> that Barkeep does not serve is refused outright. Browsers attach
/// it to cross-site requests and to every WebSocket upgrade, which is the one place the
/// same-origin policy would not have helped.</item>
/// <item>A request from loopback is trusted, exactly as it has been since M0. Anything that
/// can reach loopback is already running code on the theater PC, and the acceptance harness
/// depends on it.</item>
/// <item>On the LAN, plain HTTP serves the onboarding surface and redirects everything else
/// to TLS. The control surface exists on one port and it is encrypted.</item>
/// <item>The live socket takes a single-use ticket, because a browser cannot send a header
/// on a WebSocket.</item>
/// <item>The app shell is public and its data is not. A GET outside <c>/api</c> and
/// <c>/ws</c> is the client bundle, which an unpaired iPad has to load in order to have
/// anywhere to type its pairing code. It is JavaScript and markup, it carries no theater
/// state, and treating it as a secret would make pairing impossible.</item>
/// <item>Everything else takes a paired device's bearer token.</item>
/// </list>
///
/// There are no cookies anywhere in Cantina, which is what makes cross-site request forgery
/// structurally impossible rather than merely defended against: a hostile page cannot make a
/// browser attach a credential Barkeep never issued to it (D-026).
/// </summary>
public sealed class CantinaAccessMiddleware(
    RequestDelegate next,
    TheaterEndpoints endpoints,
    DeviceRegistry devices,
    LiveTicketStore tickets,
    TimeProvider clock)
{
    /// <summary>The key the rest of the pipeline reads the authenticated device from.</summary>
    public const string DeviceItemKey = "cantina.device";

    private static readonly string[] OnboardingPaths =
    [
        "/api/onboarding",
        "/onboarding",
        "/" + TheaterCertificateAuthority.AuthorityPublicFileName,
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";

        if (context.Request.Headers.Origin.Count > 0 &&
            !endpoints.AllowedOrigins.Contains(context.Request.Headers.Origin.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            await Refuse(context, StatusCodes.Status403Forbidden, "origin-not-served");
            return;
        }

        var remote = context.Connection.RemoteIpAddress;

        if (remote is null || IPAddress.IsLoopback(remote))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        var onboarding = OnboardingPaths.Contains(path, StringComparer.OrdinalIgnoreCase);

        if (!context.Request.IsHttps)
        {
            if (onboarding)
            {
                await next(context);
                return;
            }

            // 307, not 302: a redirect that turns a POST into a GET would drop a command on
            // the floor and report success.
            var host = context.Request.Host.Host;
            context.Response.Redirect(
                $"https://{host}:{endpoints.SecurePort}{context.Request.Path}{context.Request.QueryString}",
                permanent: false,
                preserveMethod: true);
            return;
        }

        if (onboarding || path.Equals("/api/pair", StringComparison.OrdinalIgnoreCase) || IsAppShell(context, path))
        {
            await next(context);
            return;
        }

        var now = clock.GetUtcNow();

        if (context.WebSockets.IsWebSocketRequest)
        {
            var deviceId = tickets.Redeem(context.Request.Query["ticket"], now);

            if (deviceId is null)
            {
                await Refuse(context, StatusCodes.Status401Unauthorized, "ticket-required");
                return;
            }

            context.Items[DeviceItemKey] = deviceId;
            await next(context);
            return;
        }

        var presented = BearerToken(context);
        var device = devices.Authenticate(presented, now);

        if (device is null)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer realm=\"Cantina\"";
            await Refuse(context, StatusCodes.Status401Unauthorized, "pairing-required");
            return;
        }

        context.Items[DeviceItemKey] = device.DeviceId;
        await next(context);
    }

    /// <summary>
    /// The client bundle: any GET that is not the API or the socket. Barkeep serves the
    /// iPad its own app, so this has to be reachable before a device is paired. Only GET
    /// and HEAD qualify — nothing outside <c>/api</c> mutates anything, and a POST to a
    /// static path is not a request Barkeep should be answering at all.
    /// </summary>
    private static bool IsAppShell(HttpContext context, string path) =>
        (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) &&
        !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/ws/", StringComparison.OrdinalIgnoreCase);

    private static string? BearerToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();

        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : null;
    }

    /// <summary>
    /// A named refusal and nothing else. No echo of what was presented, no host, no path —
    /// a refusal that describes the request is a refusal that helps whoever sent it.
    /// </summary>
    private static async Task Refuse(HttpContext context, int status, string reason)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"reason\":\"{reason}\"}}");
    }
}
