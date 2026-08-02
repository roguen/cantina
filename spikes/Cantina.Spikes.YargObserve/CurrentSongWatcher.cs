// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Globalization;

namespace Cantina.Spikes.YargObserve;

/// <summary>
/// Polls YARG's <c>currentSong.json</c> and <c>currentSong.txt</c>.
///
/// These files sit beside YARG's settings and were both zero bytes while the game was
/// closed. If YARG populates them during play they supply the one thing the UDP datagram
/// does not carry: the identity of the song actually playing. That would remove the main
/// justification for an upstream observation hook, so this spike measures it rather than
/// assuming it either way.
///
/// The watcher only reads. It never writes to YARG's directory.
/// </summary>
internal sealed class CurrentSongWatcher
{
    private readonly string _directory;
    private readonly TimeSpan _interval;
    private readonly Dictionary<string, string> _last = new(StringComparer.OrdinalIgnoreCase);

    public CurrentSongWatcher(string directory, TimeSpan interval)
    {
        _directory = directory;
        _interval = interval;
    }

    /// <summary>Raised when a watched file's content changes, including the first non-empty read.</summary>
    public event Action<string, string>? ContentChanged;

    /// <summary>Raised once per file if it cannot be read, so a permission problem is not silent.</summary>
    public event Action<string, string>? ReadFailed;

    public IReadOnlyCollection<string> WatchedFiles { get; } = ["currentSong.json", "currentSong.txt"];

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        foreach (var name in WatchedFiles)
        {
            _last[name] = string.Empty;
        }

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (var name in WatchedFiles)
            {
                Poll(name);
            }
        }
    }

    private void Poll(string name)
    {
        var path = Path.Combine(_directory, name);
        string content;

        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            // YARG may hold the file open; share everything so a live write is not blocked.
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            content = reader.ReadToEnd();
        }
        catch (IOException ex)
        {
            ReadFailed?.Invoke(name, ex.Message);
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            ReadFailed?.Invoke(name, ex.Message);
            return;
        }

        if (string.Equals(_last[name], content, StringComparison.Ordinal))
        {
            return;
        }

        _last[name] = content;
        ContentChanged?.Invoke(name, content);
    }

    /// <summary>Collapses content to a single log-safe line, truncated so a large payload cannot flood the console.</summary>
    public static string Summarize(string content, int maxLength = 400)
    {
        if (content.Length == 0)
        {
            return "<empty>";
        }

        var flattened = string.Join(
            ' ',
            content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return flattened.Length <= maxLength
            ? flattened
            : string.Concat(
                flattened.AsSpan(0, maxLength),
                $"... [{flattened.Length.ToString(CultureInfo.InvariantCulture)} chars total]");
    }
}
