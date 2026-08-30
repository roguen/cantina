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

    /// <summary>
    /// Whether a requester may name the destination (D-035, the owner's explicit
    /// widening of D-033). Off, codes go only to <see cref="To"/>. On, a code can be
    /// mailed to the address typed on the pairing screen - and every such send also
    /// mails the operator a notification naming the address and the requesting device,
    /// which is the compensating control that keeps an unexpected grant visible.
    /// </summary>
    public bool AllowRequesterAddresses { get; set; }

    /// <summary>SMTP AUTH identity, required by the house host for relayed (external)
    /// destinations. Empty means unauthenticated local delivery only.</summary>
    public string SmtpUsername { get; set; } = string.Empty;

    /// <summary>
    /// File holding the SMTP password - a path, never the value, so the secret can
    /// never reach configuration dumps, logs, or transcripts.
    /// </summary>
    public string SmtpPasswordPath { get; set; } = string.Empty;

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

/// <summary>What the pairing screen sends: nothing, or the address the requester wants
/// the code at (honored only when the operator has allowed that, D-035).</summary>
public sealed record PairingEmailRequest(string? Email);

/// <summary>What became of an email request.</summary>
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

    public async Task<PairingEmailStatus> RequestAsync(
        string requester, string? requestedAddress, CancellationToken cancellation)
    {
        if (!options.Value.Enabled)
        {
            return new("refused", "emailed pairing codes are not configured");
        }

        string? destination = null;

        if (!string.IsNullOrWhiteSpace(requestedAddress))
        {
            if (!options.Value.AllowRequesterAddresses)
            {
                return new("refused", "codes are emailed only to the operator's configured address");
            }

            if (!System.Net.Mail.MailAddress.TryCreate(requestedAddress.Trim(), out var parsed))
            {
                return new("refused", "that does not look like an email address");
            }

            destination = parsed.Address;
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
                options.Value.From, destination ?? options.Value.To,
                "Cantina pairing code", body, cancellation).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or System.Net.Sockets.SocketException
            or System.Security.Authentication.AuthenticationException or InvalidOperationException or OperationCanceledException)
        {
            return new("failed", $"the email could not be sent: {error.Message}");
        }

        if (destination is null)
        {
            return new("sent", "a pairing code was emailed to the operator's configured address");
        }

        // The compensating control (D-035): a requester-addressed grant is never silent.
        // The operator learns which address got a code and from which device, so an
        // unexpected email is itself the alarm. A failed copy does not unsend the code -
        // it is named instead.
        try
        {
            await transport.SendAsync(
                options.Value.From, options.Value.To,
                "Cantina pairing code sent",
                $"A pairing code was emailed to {destination}, requested by {requester}.\n\n"
                + "If nobody you know asked for this, open Cantina on the theater PC and "
                + "close the pairing window; the code also expires on its own.",
                cancellation).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or System.Net.Sockets.SocketException
            or System.Security.Authentication.AuthenticationException or InvalidOperationException or OperationCanceledException)
        {
            return new("sent",
                $"the code was emailed to {destination}, but the operator's notification copy "
                + $"failed: {error.Message}");
        }

        return new("sent", $"a pairing code was emailed to {destination}");
    }
}
