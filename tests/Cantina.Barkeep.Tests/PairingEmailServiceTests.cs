// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Access;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The emailed-code path (D-033) against a scripted transport: composition, the small
/// ceiling, window reuse, and the honest failure when the mail host lets it down.
/// </summary>
public sealed class PairingEmailServiceTests
{
    private sealed class ScriptedTransport : IPairingMailTransport
    {
        public List<(string Recipient, string Body)> Sent { get; } = [];

        public Exception? Throws { get; set; }

        public Task SendAsync(string sender, string recipient, string subject, string body, CancellationToken cancellation)
        {
            if (Throws is not null)
            {
                throw Throws;
            }

            Sent.Add((recipient, body));
            return Task.CompletedTask;
        }
    }

    private static PairingEmailOptions Configured(int perHour = 3) => new()
    {
        To = "operator@example.test",
        From = "cantina@example.test",
        SmtpHost = "mail.example.test",
        RequestsPerHour = perHour,
    };

    [Fact]
    public async Task AnUnconfiguredServiceRefusesByName()
    {
        var service = new PairingEmailService(
            new PairingWindow(), new ScriptedTransport(),
            Options.Create(new PairingEmailOptions()), TimeProvider.System);

        var status = await service.RequestAsync("192.168.68.129", CancellationToken.None);

        Assert.Equal("refused", status.State);
        Assert.Contains("not configured", status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheEmailCarriesTheLiveCodeAndNamesTheRequester()
    {
        var window = new PairingWindow();
        var transport = new ScriptedTransport();
        var service = new PairingEmailService(
            window, transport, Options.Create(Configured()), TimeProvider.System);

        var status = await service.RequestAsync("192.168.68.129", CancellationToken.None);

        Assert.Equal("sent", status.State);
        var (recipient, body) = Assert.Single(transport.Sent);
        Assert.Equal("operator@example.test", recipient);
        Assert.Contains(window.Current(DateTimeOffset.UtcNow)!.Code, body, StringComparison.Ordinal);
        Assert.Contains("192.168.68.129", body, StringComparison.Ordinal);

        // The address never leaks back to the requesting device.
        Assert.DoesNotContain("operator@example.test", status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOpenWindowIsReusedNotReplaced()
    {
        // A code the operator just read at the console must survive the tap on the iPad.
        var window = new PairingWindow();
        var existing = window.Open(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        var transport = new ScriptedTransport();
        var service = new PairingEmailService(
            window, transport, Options.Create(Configured()), TimeProvider.System);

        await service.RequestAsync("requester", CancellationToken.None);

        Assert.Contains(existing.Code, Assert.Single(transport.Sent).Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCeilingRefusesAndPointsAtTheConsole()
    {
        var service = new PairingEmailService(
            new PairingWindow(), new ScriptedTransport(),
            Options.Create(Configured(perHour: 1)), TimeProvider.System);

        Assert.Equal("sent", (await service.RequestAsync("r", CancellationToken.None)).State);

        var second = await service.RequestAsync("r", CancellationToken.None);

        Assert.Equal("refused", second.State);
        Assert.Contains("printed at the theater PC", second.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedSendIsNamedNotSwallowed()
    {
        var transport = new ScriptedTransport { Throws = new IOException("connection reset") };
        var service = new PairingEmailService(
            new PairingWindow(), transport, Options.Create(Configured()), TimeProvider.System);

        var status = await service.RequestAsync("r", CancellationToken.None);

        Assert.Equal("failed", status.State);
        Assert.Contains("connection reset", status.Detail, StringComparison.Ordinal);
    }
}
