// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Cantina.Barkeep.Access;

/// <summary>
/// Single-use, short-lived tickets for the live socket.
///
/// A browser cannot set an Authorization header on a WebSocket, and the alternatives —
/// the token in the query string, or smuggled through Sec-WebSocket-Protocol — both put a
/// long-lived credential somewhere it gets logged. So a paired device spends its token on a
/// normal authenticated POST, receives a ticket good for one connection and thirty seconds,
/// and puts that in the URL instead. A ticket in a proxy log is a ticket that has already
/// been spent (D-026).
/// </summary>
public sealed class LiveTicketStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, Ticket> _tickets = new(StringComparer.Ordinal);

    public (string Ticket, DateTimeOffset ExpiresAt) Issue(string deviceId, DateTimeOffset now)
    {
        Sweep(now);

        var value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var expiresAt = now.Add(Lifetime);

        _tickets[value] = new Ticket(deviceId, expiresAt);
        return (value, expiresAt);
    }

    /// <summary>Spend a ticket. A second attempt with the same value fails, whatever the first one did.</summary>
    public string? Redeem(string? value, DateTimeOffset now)
    {
        Sweep(now);

        if (string.IsNullOrWhiteSpace(value) || !_tickets.TryRemove(value, out var ticket))
        {
            return null;
        }

        return now < ticket.ExpiresAt ? ticket.DeviceId : null;
    }

    private void Sweep(DateTimeOffset now)
    {
        foreach (var entry in _tickets)
        {
            if (now >= entry.Value.ExpiresAt)
            {
                _tickets.TryRemove(entry.Key, out _);
            }
        }
    }

    private sealed record Ticket(string DeviceId, DateTimeOffset ExpiresAt);
}
