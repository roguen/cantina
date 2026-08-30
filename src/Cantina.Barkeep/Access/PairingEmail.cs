// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Access;

/// <summary>
/// Emailed pairing codes (D-033). Off unless the operator configures every field — and
/// the destination is **operator configuration, never client input**: a requester-typed
/// address would let any device on the LAN mail itself a code, which is the whole trust
/// model inverted. With the destination fixed, the email path substitutes "can read the
/// operator's inbox" for "is standing at the theater PC", which is a widening D-033
/// records with its compensating controls: a small hourly ceiling, the requester's
/// address in the message, and the console still printing every code.
/// </summary>
public sealed class PairingEmailOptions
{
    public const string SectionName = "PairingEmail";

    /// <summary>Where codes go. Empty disables the whole surface.</summary>
    public string To { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;

    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 25;

    public bool UseStartTls { get; set; } = true;

    /// <summary>
    /// The name EHLO announces. The house mail host rejects a bare machine name by
    /// policy (need fully-qualified hostname), so empty resolves to the sender's domain,
    /// which is fully qualified by construction.
    /// </summary>
    public string HelloName { get; set; } = string.Empty;

    /// <summary>Emailed codes per rolling hour. Pairing is rare; asking often is a signal.</summary>
    public int RequestsPerHour { get; set; } = 3;

    public string ResolveHelloName()
    {
        if (HelloName.Length > 0)
        {
            return HelloName;
        }

        var at = From.IndexOf('@', StringComparison.Ordinal);
        return at >= 0 && at < From.Length - 1 ? From[(at + 1)..] : "cantina.invalid";
    }

    public bool Enabled => To.Length > 0 && From.Length > 0 && SmtpHost.Length > 0;
}

/// <summary>What the client learns about the email surface: that it exists.</summary>
public sealed record PairingEmailView(bool Enabled);

/// <summary>What became of an email request. The address is deliberately not in it.</summary>
public sealed record PairingEmailStatus(string State, string Detail);

/// <summary>The wire seam, so the composition and ceiling are testable without SMTP.</summary>
public interface IPairingMailTransport
{
    Task SendAsync(string sender, string recipient, string subject, string body, CancellationToken cancellation);
}

/// <summary>
/// Opens (or reuses) the pairing window and mails its code to the operator's configured
/// address. Reuse matters: a code the operator just read at the console must not be
/// invalidated by a tap on the iPad.
/// </summary>
public sealed class PairingEmailService(
    PairingWindow window,
    IPairingMailTransport transport,
    IOptions<PairingEmailOptions> options,
    TimeProvider clock)
{
    private readonly ConcurrentQueue<DateTimeOffset> _recentSends = new();

    public async Task<PairingEmailStatus> RequestAsync(string requester, CancellationToken cancellation)
    {
        if (!options.Value.Enabled)
        {
            return new("refused", "emailed pairing codes are not configured");
        }

        var now = clock.GetUtcNow();

        while (_recentSends.TryPeek(out var oldest) && now - oldest > TimeSpan.FromHours(1))
        {
            _recentSends.TryDequeue(out _);
        }

        if (_recentSends.Count >= options.Value.RequestsPerHour)
        {
            return new("refused",
                $"the ceiling of {options.Value.RequestsPerHour} emailed codes per hour is reached; "
                + "the code is also printed at the theater PC");
        }

        var state = window.Current(now) ?? window.Open(now, TimeSpan.FromMinutes(10));

        var body =
            $"A device on the theater network asked to pair with Cantina.\n\n"
            + $"Pairing code: {state.Code}\n"
            + $"Valid until:  {state.ExpiresAt:HH:mm:ss zzz}\n\n"
            + $"Requested by: {requester}\n\n"
            + "If nobody you know asked for this, ignore it — the code expires on its own "
            + "and the window closes after five wrong attempts.";

        try
        {
            _recentSends.Enqueue(now);
            await transport.SendAsync(
                options.Value.From, options.Value.To,
                "Cantina pairing code", body, cancellation).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or System.Net.Sockets.SocketException
            or System.Security.Authentication.AuthenticationException or InvalidOperationException or OperationCanceledException)
        {
            return new("failed", $"the email could not be sent: {error.Message}");
        }

        return new("sent", "a pairing code was emailed to the operator's configured address");
    }
}
