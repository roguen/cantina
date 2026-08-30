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
    public async Task<string?> SendAsync(string sender, string recipient, string subject, string body, CancellationToken cancellation)
    {
        var config = options.Value;
        var message = ComposeMessage(sender, recipient, subject, body);

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

            // The house host relays external destinations only for authenticated senders
            // (D-035); local delivery needs no identity. The password comes from a file at
            // send time, so the secret lives in exactly one place.
            if (config.SmtpUsername.Length > 0 && config.SmtpPasswordPath.Length > 0)
            {
                var password = (await File.ReadAllTextAsync(config.SmtpPasswordPath, lifetime.Token).ConfigureAwait(false)).Trim();
                var identity = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"\0{config.SmtpUsername}\0{password}"));
                await session.CommandAsync($"AUTH PLAIN {identity}", "235", "AUTH").ConfigureAwait(false);
            }

            await session.CommandAsync($"MAIL FROM:<{sender}>", "250", "MAIL FROM").ConfigureAwait(false);
            await session.CommandAsync($"RCPT TO:<{recipient}>", "250", "RCPT TO").ConfigureAwait(false);
            await session.CommandAsync("DATA", "354", "DATA").ConfigureAwait(false);

            // Dot-stuffing, so a line of the message can never end the DATA section early.
            var stuffed = new StringBuilder();

            foreach (var line in message.ReplaceLineEndings("\n").Split('\n'))
            {
                stuffed.Append(line.StartsWith('.') ? "." + line : line).Append("\r\n");
            }

            await session.CommandAsync(stuffed + ".", "250", "message body").ConfigureAwait(false);
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

        // The mailbox should show what the system sent (operator request, 2026-08-30):
        // servers submitting over SMTP file nothing into Sent on their own, so this
        // client does it itself over IMAP. A failed filing never unsends the message —
        // it comes back as a warning sentence instead. Without credentials there is no
        // mailbox identity to file into, and nothing is owed.
        if (config.SmtpUsername.Length == 0 || config.SmtpPasswordPath.Length == 0)
        {
            return null;
        }

        try
        {
            await FileToSentAsync(message, cancellation).ConfigureAwait(false);
            return null;
        }
        catch (Exception error) when (error is IOException or SocketException
            or System.Security.Authentication.AuthenticationException or InvalidOperationException or OperationCanceledException)
        {
            return $"the copy could not be filed to Sent: {error.Message}";
        }
    }

    private static string ComposeMessage(string sender, string recipient, string subject, string body) =>
        new StringBuilder()
            .Append("From: ").Append(sender).Append("\r\n")
            .Append("To: ").Append(recipient).Append("\r\n")
            .Append("Subject: ").Append(subject.ReplaceLineEndings(" ")).Append("\r\n")
            .Append("Date: ").Append(DateTimeOffset.Now.ToString("r", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n")
            .Append("MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n")
            .Append(body.ReplaceLineEndings("\r\n"))
            .ToString();

    private async Task FileToSentAsync(string message, CancellationToken cancellation)
    {
        var config = options.Value;
        var password = (await File.ReadAllTextAsync(config.SmtpPasswordPath, cancellation).ConfigureAwait(false)).Trim();

        using var client = new TcpClient();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        lifetime.CancelAfter(TimeSpan.FromSeconds(20));

        await client.ConnectAsync(config.SmtpHost, config.ImapPort, lifetime.Token).ConfigureAwait(false);

        var tls = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        await tls.AuthenticateAsClientAsync(
            new SslClientAuthenticationOptions { TargetHost = config.SmtpHost },
            lifetime.Token).ConfigureAwait(false);

        using var reader = new StreamReader(tls, Encoding.ASCII, false, 1024, leaveOpen: true);
        var payload = Encoding.UTF8.GetBytes(message);

        async Task<string> RoundTrip(string tag, string command)
        {
            var bytes = Encoding.ASCII.GetBytes($"{tag} {command}\r\n");
            await tls.WriteAsync(bytes, lifetime.Token).ConfigureAwait(false);
            string? line;

            do
            {
                line = await reader.ReadLineAsync(lifetime.Token).ConfigureAwait(false)
                    ?? throw new IOException($"the mail server hung up during {tag}");
            }
            while (!line.StartsWith(tag + " ", StringComparison.Ordinal));

            return line;
        }

        _ = await reader.ReadLineAsync(lifetime.Token).ConfigureAwait(false);   // greeting

        if (!(await RoundTrip("a1", $"LOGIN {config.SmtpUsername} {password}").ConfigureAwait(false)).Contains(" OK", StringComparison.Ordinal))
        {
            throw new IOException("IMAP login was refused");
        }

        // CREATE is idempotent enough here: an already-existing Sent answers NO, which
        // is exactly the state APPEND needs.
        _ = await RoundTrip("a2", "CREATE Sent").ConfigureAwait(false);

        var appendBytes = Encoding.ASCII.GetBytes($"a3 APPEND Sent (\\Seen) {{{payload.Length}}}\r\n");
        await tls.WriteAsync(appendBytes, lifetime.Token).ConfigureAwait(false);
        var go = await reader.ReadLineAsync(lifetime.Token).ConfigureAwait(false) ?? "";

        if (!go.StartsWith('+'))
        {
            throw new IOException($"APPEND was refused: {go}");
        }

        await tls.WriteAsync(payload, lifetime.Token).ConfigureAwait(false);
        await tls.WriteAsync(Encoding.ASCII.GetBytes("\r\n"), lifetime.Token).ConfigureAwait(false);
        string? done;

        do
        {
            done = await reader.ReadLineAsync(lifetime.Token).ConfigureAwait(false)
                ?? throw new IOException("the mail server hung up during APPEND");
        }
        while (!done.StartsWith("a3 ", StringComparison.Ordinal));

        if (!done.Contains(" OK", StringComparison.Ordinal))
        {
            throw new IOException($"APPEND failed: {done}");
        }

        _ = await RoundTrip("a4", "LOGOUT").ConfigureAwait(false);
        await tls.DisposeAsync().ConfigureAwait(false);
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
