// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cantina.Barkeep.Access;

/// <summary>
/// The paired devices, and the only place a credential is checked.
///
/// A token is 32 bytes from <see cref="RandomNumberGenerator"/>, handed to the device once
/// and never stored: the registry keeps a SHA-256 hash and compares in fixed time. A stolen
/// registry file therefore yields no working credential, and the file can be read for an
/// audit without becoming one. Revocation is deletion; rotation is revocation plus a new
/// grant for the same label, which is what "the iPad was replaced" actually means.
///
/// Persistence follows the journal's rule (D-023): written and flushed before the caller is
/// answered, and replaced atomically, because this host has no graceful shutdown to write
/// during.
/// </summary>
public sealed class DeviceRegistry
{
    private const string FileName = "paired-devices.json";
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly List<StoredDevice> _devices = [];

    private DeviceRegistry(string path) => _path = path;

    public static DeviceRegistry Open(string directory)
    {
        Directory.CreateDirectory(directory);
        var registry = new DeviceRegistry(Path.Combine(directory, FileName));
        registry.Load();
        return registry;
    }

    public IReadOnlyList<PairedDevice> Devices
    {
        get
        {
            lock (_gate)
            {
                return [.. _devices.Select(device =>
                    new PairedDevice(device.DeviceId, device.Label, device.PairedAt, device.LastSeenAt))];
            }
        }
    }

    public bool AnyPaired
    {
        get
        {
            lock (_gate)
            {
                return _devices.Count > 0;
            }
        }
    }

    /// <summary>Mint a credential for a newly paired device. The plaintext token is returned once and never again.</summary>
    public PairingGrant Grant(string label, DateTimeOffset now)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var deviceId = Base64Url(RandomNumberGenerator.GetBytes(9));
        var stored = new StoredDevice
        {
            DeviceId = deviceId,
            Label = string.IsNullOrWhiteSpace(label) ? "iPad" : label.Trim(),
            TokenHash = Hash(token),
            PairedAt = now,
            LastSeenAt = null,
        };

        lock (_gate)
        {
            _devices.Add(stored);
            Save();
        }

        return new PairingGrant(stored.DeviceId, stored.Label, token, stored.PairedAt);
    }

    /// <summary>
    /// Resolve a presented token. Every stored hash is compared in fixed time and the loop
    /// does not stop at the first match, so a caller learns nothing from how long this took.
    /// </summary>
    public PairedDevice? Authenticate(string? presented, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(presented))
        {
            return null;
        }

        var candidate = Hash(presented);
        StoredDevice? matched = null;

        lock (_gate)
        {
            foreach (var device in _devices)
            {
                if (CryptographicOperations.FixedTimeEquals(device.TokenHash, candidate))
                {
                    matched = device;
                }
            }

            if (matched is null)
            {
                return null;
            }

            // Last-seen is a convenience for the operator, not a security record. It is
            // written at most once a minute so an active iPad does not turn the registry
            // into a write loop.
            if (matched.LastSeenAt is null || now - matched.LastSeenAt.Value > TimeSpan.FromMinutes(1))
            {
                matched.LastSeenAt = now;
                Save();
            }

            return new PairedDevice(matched.DeviceId, matched.Label, matched.PairedAt, matched.LastSeenAt);
        }
    }

    public bool Revoke(string deviceId)
    {
        lock (_gate)
        {
            var removed = _devices.RemoveAll(device =>
                string.Equals(device.DeviceId, deviceId, StringComparison.Ordinal));

            if (removed == 0)
            {
                return false;
            }

            Save();
            return true;
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<List<StoredDevice>>(File.ReadAllText(_path), Json);

            if (stored is not null)
            {
                _devices.AddRange(stored.Where(device =>
                    !string.IsNullOrWhiteSpace(device.DeviceId) && device.TokenHash.Length == 32));
            }
        }
        catch (JsonException)
        {
            // A registry that will not parse is quarantined rather than trusted or
            // truncated: every device re-pairs, which is safe, visible, and recoverable at
            // the theater PC.
            File.Move(_path, $"{_path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}", overwrite: false);
        }
    }

    private void Save()
    {
        var temp = _path + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(_devices, Json);

        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, _path, overwrite: true);
    }

    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class StoredDevice
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public byte[] TokenHash { get; set; } = [];
        public DateTimeOffset PairedAt { get; set; }
        public DateTimeOffset? LastSeenAt { get; set; }
    }
}
