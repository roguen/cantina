// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Net.Sockets;
using System.Text;
using Cantina.Barkeep.Access;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The hand-rolled SMTP dialog against a scripted server that behaves like the house
/// mail host actually behaved (measured 2026-08-30): a postscreen-style multi-line
/// greeting on first contact, and EHLO rejected unless the name is fully qualified.
/// Both cost a live debugging session; both are pinned here.
/// </summary>
public sealed class SmtpTransportTests
{
    private sealed class ScriptedSmtpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serving;

        public List<string> Received { get; } = [];

        public int Port { get; }

        public ScriptedSmtpServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serving = ServeAsync();
        }

        public void Dispose() => _listener.Dispose();

        public void AssertServed() => _serving.GetAwaiter().GetResult();

        private async Task ServeAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            // The postscreen shape: a partial greeting first, then the real one.
            await writer.WriteLineAsync("220-mail.test ESMTP");
            await writer.WriteLineAsync("220 mail.test ESMTP");

            var ehlo = await reader.ReadLineAsync() ?? "";
            Received.Add(ehlo);

            // The house policy that rejected SmtpClient: a HELO name with no dot in it
            // is not fully qualified.
            if (!ehlo.StartsWith("EHLO ", StringComparison.Ordinal) || !ehlo["EHLO ".Length..].Contains('.'))
            {
                await writer.WriteLineAsync("501 5.5.2 Helo command rejected: need fully-qualified hostname");
                return;
            }

            await writer.WriteLineAsync("250-mail.test");
            await writer.WriteLineAsync("250 8BITMIME");

            while (await reader.ReadLineAsync() is { } line)
            {
                Received.Add(line);

                if (line.StartsWith("MAIL FROM", StringComparison.Ordinal)
                    || line.StartsWith("RCPT TO", StringComparison.Ordinal))
                {
                    await writer.WriteLineAsync("250 Ok");
                }
                else if (line == "DATA")
                {
                    await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                }
                else if (line == ".")
                {
                    await writer.WriteLineAsync("250 Ok: queued");
                }
                else if (line == "QUIT")
                {
                    await writer.WriteLineAsync("221 Bye");
                    return;
                }
            }
        }
    }

    [Fact]
    public async Task TheDialogSurvivesThePostscreenGreetingAndAnnouncesAnFqdn()
    {
        using var server = new ScriptedSmtpServer();
        var transport = new SmtpPairingMailTransport(Options.Create(new PairingEmailOptions
        {
            To = "operator@example.test",
            From = "cantina@aero4ge.test",
            SmtpHost = "127.0.0.1",
            SmtpPort = server.Port,
            UseStartTls = false,
        }));

        await transport.SendAsync(
            "cantina@aero4ge.test", "operator@example.test",
            "Cantina pairing code", "Pairing code: ABCD2345\n.hidden dot line", CancellationToken.None);

        server.AssertServed();

        // Empty HelloName resolves to the sender's domain — fully qualified by construction.
        Assert.Equal("EHLO aero4ge.test", server.Received[0]);
        Assert.Contains("MAIL FROM:<cantina@aero4ge.test>", server.Received);
        Assert.Contains("RCPT TO:<operator@example.test>", server.Received);

        // Dot-stuffing: the body line beginning with '.' arrives escaped, not as the
        // end-of-data marker.
        Assert.Contains("..hidden dot line", server.Received);
        Assert.Contains("Subject: Cantina pairing code", server.Received);
    }

    [Fact]
    public async Task ARefusedStepThrowsWithTheServersOwnWords()
    {
        using var server = new ScriptedSmtpServer();
        var transport = new SmtpPairingMailTransport(Options.Create(new PairingEmailOptions
        {
            To = "operator@example.test",
            From = "cantina@aero4ge.test",
            SmtpHost = "127.0.0.1",
            SmtpPort = server.Port,
            UseStartTls = false,
            HelloName = "nodots",
        }));

        var error = await Assert.ThrowsAsync<IOException>(() => transport.SendAsync(
            "cantina@aero4ge.test", "operator@example.test", "s", "b", CancellationToken.None));

        Assert.Contains("need fully-qualified hostname", error.Message, StringComparison.Ordinal);
    }
}
