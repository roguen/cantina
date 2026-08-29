// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// A Barkeep configured as if it were on the LAN, and the two facts a test server cannot
/// supply on its own: which address a request came from, and whether it arrived over TLS.
///
/// Both are headers here rather than real sockets, which is honest about what this proves.
/// It proves the decision logic — refusal, redirect, credential, ticket — against a real
/// pipeline. It does not prove the socket binding or the TLS handshake; those are the
/// target-PC acceptance run's job, because a test server has neither.
/// </summary>
internal static class LanTestHost
{
    public const string LanAddress = "192.0.2.24";
    public const string SecureOrigin = "https://192.0.2.24:5274";

    public static WebApplicationFactory<Program> Create(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder
            .UseSetting("Yarg:Enabled", "false")
            .UseSetting("Network:Mode", "Lan")
            .UseSetting("Network:Address", LanAddress)
            .UseSetting("Setlist:DataDirectory",
                Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName()))
            .ConfigureServices(services =>
                services.AddSingleton<IStartupFilter, RequestOriginFilter>()));

    /// <summary>A request that arrives the way an iPad's does: from the LAN, over TLS.</summary>
    public static HttpRequestMessage FromLan(HttpMethod method, string path, string remote = "192.0.2.99")
    {
        var request = FromLanPlain(method, path, remote);
        request.Headers.Add("X-Test-Scheme", "https");
        return request;
    }

    /// <summary>The same request before the device trusts anything: LAN, no TLS.</summary>
    public static HttpRequestMessage FromLanPlain(HttpMethod method, string path, string remote = "192.0.2.99")
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Host = LanAddress;
        request.Headers.Add("X-Test-Remote", remote);
        return request;
    }

    internal sealed class RequestOriginFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            builder =>
            {
                builder.Use(async (context, proceed) =>
                {
                    if (context.Request.Headers.TryGetValue("X-Test-Remote", out var remote))
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse(remote.ToString());
                    }

                    if (context.Request.Headers.TryGetValue("X-Test-Scheme", out var scheme))
                    {
                        context.Request.Scheme = scheme.ToString();
                    }

                    await proceed();
                });

                next(builder);
            };
    }
}
