// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Access;

/// <summary>
/// A deliberately small SMTP client, because System.Net.Mail.SmtpClient cannot be told
/// its own name: it announces the bare machine name in EHLO, and the house mail host
/// rejects non-FQDN hellos by policy (measured live 2026-08-30:
/// "5.5.2 Helo command rejected: need fully-qualified hostname"). The host also runs a
/// postscreen-style multi-line greeting test on first contact, which this client waits
/// out like the RFC says to.
///
/// It speaks exactly the dialog the live probe proved: greeting, EHLO with an FQDN,
/// optional STARTTLS, MAIL FROM, RCPT TO, DATA, QUIT. Anything unexpected throws with
/// the server's own words, which is what the iPad ends up reading.
/// </summary>
public sealed class SmtpPairingMailTransport(IOptions<PairingEmailOptions> options) : IPairingMailTransport
{
    public async Task SendAsync(string sender, string recipient, string subject, string body, CancellationToken cancellation)
    {
        var config = options.Value;

        using var client = new TcpClient();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        lifetime.CancelAfter(TimeSpan.FromSeconds(30));

        await client.ConnectAsync(config.SmtpHost, config.SmtpPort, lifetime.Token).ConfigureAwait(false);

        Stream stream = client.GetStream();
        var session = new Session(stream, lifetime.Token);

        try
        {

            await session.ExpectAsync("220", "greeting").ConfigureAwait(false);
            await session.CommandAsync($"EHLO {config.ResolveHelloName()}", "250", "EHLO").ConfigureAwait(false);

            if (config.UseStartTls)
            {
                await session.CommandAsync("STARTTLS", "220", "STARTTLS").ConfigureAwait(false);

                var secure = new SslStream(stream, leaveInnerStreamOpen: false);
                await secure.AuthenticateAsClientAsync(
                    new SslClientAuthenticationOptions { TargetHost = config.SmtpHost },
                    lifetime.Token).ConfigureAwait(false);
                stream = secure;
                session.Dispose();
                session = new Session(stream, lifetime.Token);

                // The session restarts clean after the handshake, per RFC 3207.
                await session.CommandAsync($"EHLO {config.ResolveHelloName()}", "250", "EHLO after STARTTLS").ConfigureAwait(false);
            }

            await session.CommandAsync($"MAIL FROM:<{sender}>", "250", "MAIL FROM").ConfigureAwait(false);
            await session.CommandAsync($"RCPT TO:<{recipient}>", "250", "RCPT TO").ConfigureAwait(false);
            await session.CommandAsync("DATA", "354", "DATA").ConfigureAwait(false);

            var message = new StringBuilder()
                .Append("From: ").Append(sender).Append("\r\n")
                .Append("To: ").Append(recipient).Append("\r\n")
                .Append("Subject: ").Append(subject.ReplaceLineEndings(" ")).Append("\r\n")
                .Append("MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n");

            // Dot-stuffing, so a line of the body can never end the DATA section early.
            foreach (var line in body.ReplaceLineEndings("\n").Split('\n'))
            {
                message.Append(line.StartsWith('.') ? "." + line : line).Append("\r\n");
            }

            await session.CommandAsync(message + ".", "250", "message body").ConfigureAwait(false);
            await session.CommandAsync("QUIT", "221", "QUIT").ConfigureAwait(false);
        }
        finally
        {
            session.Dispose();

            if (stream is SslStream tls)
            {
                await tls.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private sealed class Session(Stream stream, CancellationToken cancellation) : IDisposable
    {
        private readonly StreamReader _reader = new(stream, Encoding.ASCII, false, 1024, leaveOpen: true);

        public void Dispose() => _reader.Dispose();

        public async Task CommandAsync(string command, string expectedCode, string step)
        {
            var bytes = Encoding.ASCII.GetBytes(command + "\r\n");
            await stream.WriteAsync(bytes, cancellation).ConfigureAwait(false);
            await stream.FlushAsync(cancellation).ConfigureAwait(false);
            await ExpectAsync(expectedCode, step).ConfigureAwait(false);
        }

        /// <summary>Reads one full (possibly multi-line) reply and insists on the code.</summary>
        public async Task ExpectAsync(string expectedCode, string step)
        {
            string? line;

            do
            {
                line = await _reader.ReadLineAsync(cancellation).ConfigureAwait(false)
                    ?? throw new IOException($"the mail server hung up during {step}");
            }
            while (line.Length >= 4 && line[3] == '-');

            if (!line.StartsWith(expectedCode, StringComparison.Ordinal))
            {
                throw new IOException($"{step} was refused: {line}");
            }
        }
    }
}
