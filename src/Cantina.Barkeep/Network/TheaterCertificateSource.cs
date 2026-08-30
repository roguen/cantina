// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Cantina.Barkeep.Network;

/// <summary>
/// The certificate Kestrel is currently serving, and the machinery that notices when a
/// renewal has landed.
///
/// This exists because of a gap that would otherwise have made the whole Let's Encrypt
/// design useless: **Kestrel reads its certificate once, at startup.** The issuer renews on
/// its own schedule and delivers a new file, and without this Barkeep would go on serving
/// the old certificate until somebody happened to restart it — which is to say, until the
/// old one expired and the theater stopped working. The renewal would run perfectly and
/// change nothing. That is the same silent-success shape D-029's expiry signal exists to
/// catch, and catching it is not as good as not having it.
///
/// Two behaviours are deliberate:
///
/// **A failed reload keeps the certificate that is working.** The file arrives by `scp`,
/// which is not atomic, so a poll can catch it half-written. Dropping the live certificate
/// because a copy was in flight would turn a renewal into an outage. The failure is logged
/// and the next poll tries again.
///
/// **The initial load is still fatal.** Starting with no usable certificate is a
/// configuration error, and a server that quietly serves something other than what the
/// operator configured fails its clients in a way nobody can diagnose.
/// </summary>
public sealed class TheaterCertificateSource : IDisposable
{
    private readonly object _gate = new();
    private readonly string? _watchedPath;
    private readonly string? _keyPath;
    private readonly string? _password;
    private readonly ILogger _log;

    private TheaterCertificates _current;
    private SslStreamCertificateContext? _context;
    private (DateTime Written, long Length) _stamp;

    public TheaterCertificateSource(TheaterCertificates initial, NetworkOptions options, ILogger log)
    {
        _current = initial;
        _log = log;
        _context = ContextFor(initial, log);

        // Only a supplied certificate is watched. The theater authority reissues its own
        // leaf at startup and has a ten-year anchor, so there is nothing arriving from
        // outside to notice.
        if (!initial.NeedsDeviceTrust && !string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            _watchedPath = options.CertificatePath;
            _keyPath = options.CertificateKeyPath;
            _password = options.CertificatePassword;
            _stamp = StampOf(_watchedPath);
        }
    }

    /// <summary>The certificate in force right now, for the health surface.</summary>
    public TheaterCertificates Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>Whether anything is being watched, so a caller can report honestly rather than imply a watch that is not running.</summary>
    public bool Watching => _watchedPath is not null;

    /// <summary>
    /// What Kestrel hands the TLS stack per connection. Reading the context under the lock
    /// means a connection arriving mid-reload gets one whole certificate or the other,
    /// never a leaf with the wrong chain.
    /// </summary>
    public SslServerAuthenticationOptions ServerOptions()
    {
        lock (_gate)
        {
            // The context (leaf + intermediates, pre-built) is preferred; a bare
            // certificate is the fallback when the chain engine refused to build one.
            return _context is not null
                ? new SslServerAuthenticationOptions { ServerCertificateContext = _context }
                : new SslServerAuthenticationOptions { ServerCertificate = _current.Server };
        }
    }

    /// <summary>
    /// Reload if the watched file has changed since it was last read. Returns true when a
    /// new certificate took effect.
    /// </summary>
    public bool RefreshIfChanged()
    {
        if (_watchedPath is null)
        {
            return false;
        }

        var stamp = StampOf(_watchedPath);

        if (stamp == _stamp || stamp.Length == 0)
        {
            return false;
        }

        try
        {
            var reloaded = TheaterCertificateAuthority.LoadSupplied(_watchedPath, _password, _keyPath);
            var context = ContextFor(reloaded, _log);

            lock (_gate)
            {
                _current.Server.Dispose();
                _current = reloaded;
                _context = context;
            }

            _stamp = stamp;
            NetworkLog.CertificateReloaded(_log, reloaded.Server.Subject, reloaded.NotAfter);
            return true;
        }
        catch (Exception error) when (error is CryptographicException or IOException or InvalidOperationException)
        {
            // Half-written, or briefly unreadable. Keep serving what works and try again.
            NetworkLog.CertificateReloadFailed(_log, error.Message);
            return false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _current.Server.Dispose();
            _current.Authority?.Dispose();
        }
    }

    /// <summary>
    /// Null when Windows' chain engine refuses to build a context — observed on the
    /// theater PC on 2026-08-30 for certificates it had accepted an hour earlier, with
    /// CryptographicException("unknown chain building error"). The context only pre-packs
    /// the intermediates; serving the bare certificate is what Barkeep did for weeks
    /// before the reload work, so degrading beats dying, and the reason is logged.
    /// </summary>
    private static SslStreamCertificateContext? ContextFor(TheaterCertificates certificates, ILogger log)
    {
        try
        {
            return SslStreamCertificateContext.Create(certificates.Server, certificates.Chain);
        }
        catch (CryptographicException error)
        {
            NetworkLog.CertificateContextUnavailable(log, error.Message);
            return null;
        }
    }

    private static (DateTime Written, long Length) StampOf(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? (file.LastWriteTimeUtc, file.Length) : (default, 0);
    }
}

/// <summary>Polls for a delivered renewal. Cheap, and slow on purpose — a certificate changes twice a year.</summary>
public sealed class CertificateRenewalWatcher(TheaterCertificateSource source) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!source.Watching)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            source.RefreshIfChanged();
        }
    }
}
