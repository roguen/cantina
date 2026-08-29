// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;

namespace Cantina.Barkeep.Access;

/// <summary>
/// The gate a new device must pass, and the reason the gate is trustworthy: it can only be
/// opened from the theater PC itself, and the code it produces is shown there.
///
/// Physical presence at the theater is the trust anchor (D-026). Nothing on the LAN can
/// open a window, and nothing on the LAN can read a code, so an attacker who can reach
/// Barkeep still cannot pair without standing in the room. The window is single-use, expires
/// on a clock rather than on a request, and closes permanently after a small number of wrong
/// codes — a guessing attempt costs a trip to the theater PC to reopen.
/// </summary>
public sealed class PairingWindow
{
    // No I, L, O, U, 0, or 1: the code is read off a screen and typed on a tablet, and
    // those are the characters that get read wrong.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";
    private const int CodeLength = 8;
    private const int MaximumAttempts = 5;

    private readonly object _gate = new();
    private string? _code;
    private DateTimeOffset _expiresAt;
    private int _attemptsRemaining;

    public PairingWindowState Open(DateTimeOffset now, TimeSpan lifetime)
    {
        lock (_gate)
        {
            _code = NewCode();
            _expiresAt = now.Add(lifetime);
            _attemptsRemaining = MaximumAttempts;
            return new PairingWindowState(_code, _expiresAt, _attemptsRemaining);
        }
    }

    public PairingWindowState? Current(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_code is null || now >= _expiresAt)
            {
                return null;
            }

            return new PairingWindowState(_code, _expiresAt, _attemptsRemaining);
        }
    }

    public void Close()
    {
        lock (_gate)
        {
            _code = null;
            _attemptsRemaining = 0;
        }
    }

    /// <summary>
    /// Spend the window on a candidate code. A correct code closes it; so does running out
    /// of attempts. The comparison is fixed-time, which costs nothing and removes a whole
    /// class of argument about whether it mattered.
    /// </summary>
    public PairingResult Redeem(string? candidate, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_code is null)
            {
                return PairingResult.NoWindowOpen;
            }

            if (now >= _expiresAt)
            {
                _code = null;
                return PairingResult.Expired;
            }

            if (_attemptsRemaining <= 0)
            {
                _code = null;
                return PairingResult.TooManyAttempts;
            }

            // The length is not a secret — it is fixed and published — so checking it
            // first leaks nothing, and it lets the byte comparison stay fixed-time.
            var normalized = Normalize(candidate);
            var correct = normalized.Length == _code.Length &&
                CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(normalized),
                    Encoding.ASCII.GetBytes(_code));

            if (!correct)
            {
                _attemptsRemaining--;

                if (_attemptsRemaining <= 0)
                {
                    _code = null;
                    return PairingResult.TooManyAttempts;
                }

                return PairingResult.WrongCode;
            }

            _code = null;
            _attemptsRemaining = 0;
            return PairingResult.Accepted;
        }
    }

    /// <summary>Codes are read aloud and typed; spacing, hyphens, and case are the reader's, not the protocol's.</summary>
    private static string Normalize(string? candidate) =>
        new((candidate ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

    private static string NewCode() =>
        RandomNumberGenerator.GetString(Alphabet, CodeLength);
}
