// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Cantina.SelfTest;

/// <summary>
/// The D-026 transport, proved against a Barkeep that is actually on the LAN.
///
/// The unit tests prove the decision logic against a test server, which has no sockets and
/// no TLS. This suite proves the half a test server cannot: that the listeners bind the
/// real interface, that the handshake completes with a certificate naming that address,
/// that the chain terminates in the theater authority, and that pairing, revocation, and
/// socket reconnection behave the way the iPad will experience them.
///
/// It sends no input to YARG and never touches the game. Its only side effects on Barkeep
/// are a device it pairs and then revokes, and a setlist entry it adds under a command id
/// of its own — the entry stays, because proving a replay does not re-apply is the point.
/// </summary>
internal static class LanTransportSuite
{
    private const string Name = "lan";

    public static async Task<SuiteResult> RunAsync(Transcript transcript, string loopback)
    {
        using var local = new HttpClient { BaseAddress = new Uri(loopback) };

        OnboardingView? onboarding;

        try
        {
            onboarding = await local.GetFromJsonAsync<OnboardingView>("/api/onboarding").ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            transcript.Log("INCONCLUSIVE", $"{Name}: barkeep-not-running at {loopback} ({error.GetType().Name})");
            return new SuiteResult(Name, Verdict.Inconclusive, "Barkeep was not answering on loopback.");
        }

        // The test for "is this on the LAN" is the scheme, not the fingerprint. An earlier
        // version keyed off a non-empty fingerprint, which stopped meaning anything the day
        // a supplied certificate could be served: that has no theater authority and so no
        // fingerprint, and the suite would have called a working LAN binding loopback-only.
        if (onboarding is null || !onboarding.SecureUrl.StartsWith("https://", StringComparison.Ordinal))
        {
            transcript.Log("INCONCLUSIVE", $"{Name}: barkeep-is-loopback-only (start it with Network:Mode=Lan)");
            return new SuiteResult(Name, Verdict.Inconclusive, "Barkeep is bound to loopback, so there is no LAN transport to measure.");
        }

        var secure = new Uri(onboarding.SecureUrl);
        var plain = new UriBuilder("http", secure.Host, 5273).Uri;
        transcript.Log("SETUP", $"{Name}: secure={secure} plain={plain} needs_device_trust={onboarding.NeedsDeviceTrust}");

        // With a supplied certificate there is no theater authority to fetch, and trust is
        // the machine's own root store rather than one certificate this suite pinned.
        X509Certificate2? authority = null;

        if (onboarding.NeedsDeviceTrust)
        {
            authority = X509CertificateLoader.LoadCertificate(
                await local.GetByteArrayAsync($"/{onboarding.CertificateUrl.TrimStart('/')}").ConfigureAwait(false));
            transcript.Log("SETUP", $"{Name}: authority_subject=\"{authority.Subject}\" not_after={authority.NotAfter:yyyy-MM-dd}");
        }
        else
        {
            transcript.Log("SETUP", $"{Name}: publicly-trusted certificate, no authority to distribute");
        }

        var cases = new List<bool>();
        string? deviceId = null;

        using var handler = new SocketsHttpHandler
        {
            SslOptions = { RemoteCertificateValidationCallback = (_, certificate, _, errors) => Trusts(authority, certificate, errors) },
        };
        using var lan = new HttpClient(handler) { BaseAddress = secure };
        using var lanPlain = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { BaseAddress = plain };

        try
        {
            // 1. The unencrypted LAN port carries onboarding and nothing else.
            using var page = await lanPlain.GetAsync("/onboarding").ConfigureAwait(false);
            var html = await page.Content.ReadAsStringAsync().ConfigureAwait(false);
            cases.Add(Case(transcript, "plain-http-onboarding",
                page.StatusCode == HttpStatusCode.OK && html.Contains("Certificate Trust Settings", StringComparison.Ordinal),
                $"status={(int)page.StatusCode} bytes={html.Length}"));

            using var redirected = await lanPlain.GetAsync("/api/live").ConfigureAwait(false);
            cases.Add(Case(transcript, "plain-http-redirects-control",
                redirected.StatusCode == HttpStatusCode.TemporaryRedirect &&
                redirected.Headers.Location?.Scheme == "https",
                $"status={(int)redirected.StatusCode} location={redirected.Headers.Location}"));

            // 2. The handshake itself. The callback rejects a name mismatch outright and
            //    builds the chain against the served authority, so a pass here means the
            //    certificate both names this address and was signed by this theater.
            using var unpaired = await lan.GetAsync("/api/live").ConfigureAwait(false);
            var refusal = await unpaired.Content.ReadAsStringAsync().ConfigureAwait(false);
            cases.Add(Case(transcript, "tls-chains-to-theater-authority",
                unpaired.StatusCode == HttpStatusCode.Unauthorized && refusal.Contains("pairing-required", StringComparison.Ordinal),
                $"status={(int)unpaired.StatusCode} body={refusal}"));

            // 3. The app shell is public and its data is not. An unpaired iPad has to load
            //    the client to have anywhere to type its code; a 401 here would be a
            //    deadlock rather than a defence.
            using var shell = await lan.GetAsync("/").ConfigureAwait(false);
            cases.Add(Case(transcript, "app-shell-loads-unpaired",
                shell.StatusCode != HttpStatusCode.Unauthorized,
                $"status={(int)shell.StatusCode} bundle_present={shell.StatusCode == HttpStatusCode.OK}"));

            using var wrongVerb = await lan.PostAsync("/anything", Empty()).ConfigureAwait(false);
            cases.Add(Case(transcript, "only-reads-of-the-shell-are-public",
                wrongVerb.StatusCode == HttpStatusCode.Unauthorized,
                $"status={(int)wrongVerb.StatusCode}"));

            // 4. Pairing: the code is readable only over loopback, which is the theater PC.
            using var opened = await local.PostAsync("/api/pairing/window", Empty()).ConfigureAwait(false);
            var window = await opened.Content.ReadFromJsonAsync<WindowView>().ConfigureAwait(false);

            if (window is null)
            {
                transcript.Log("INCONCLUSIVE", $"{Name}: pairing-window-refused status={(int)opened.StatusCode}");
                return new SuiteResult(Name, Verdict.Inconclusive, "Barkeep would not open a pairing window over loopback.");
            }

            using var claimed = await lan.PostAsync("/api/pair", Json(
                $$"""{"code":"{{window.Code}}","label":"Cantina.SelfTest"}""")).ConfigureAwait(false);
            var grant = await claimed.Content.ReadFromJsonAsync<GrantView>().ConfigureAwait(false);
            deviceId = grant?.DeviceId;

            cases.Add(Case(transcript, "pairing-grants-once",
                claimed.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(grant?.Token),
                $"status={(int)claimed.StatusCode} device={deviceId}"));

            if (grant is null)
            {
                return Finish(transcript, cases, "Pairing did not produce a credential.");
            }

            lan.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", grant.Token);

            using var served = await lan.GetAsync("/api/live").ConfigureAwait(false);
            cases.Add(Case(transcript, "paired-device-is-served",
                served.StatusCode == HttpStatusCode.OK,
                $"status={(int)served.StatusCode}"));

            // 5. The live socket, with the ticket that stands in for a header a browser
            //    cannot send, and the reconnection an iPad performs every time it sleeps.
            cases.Add(await SocketCasesAsync(transcript, lan, secure, authority).ConfigureAwait(false));

            // 6. A reconnect must not replay a command. The socket carries no commands at
            //    all, so the risk lives in the client's retry: the same command id twice
            //    must converge, not duplicate.
            cases.Add(await ReplayCaseAsync(transcript, lan).ConfigureAwait(false));

            // 7. Revocation is the recovery path when the iPad is lost or replaced.
            using var revoked = await lan.DeleteAsync($"/api/devices/{deviceId}").ConfigureAwait(false);
            using var afterward = await lan.GetAsync("/api/live").ConfigureAwait(false);
            deviceId = null;

            cases.Add(Case(transcript, "revocation-is-immediate",
                revoked.StatusCode == HttpStatusCode.NoContent && afterward.StatusCode == HttpStatusCode.Unauthorized,
                $"revoke={(int)revoked.StatusCode} next_request={(int)afterward.StatusCode}"));
        }
        finally
        {
            if (deviceId is not null)
            {
                // A suite that leaves a working credential behind is a suite that widened
                // the attack surface to measure it.
                using var cleanup = await local.DeleteAsync($"/api/devices/{deviceId}").ConfigureAwait(false);
                transcript.Log("CLEANUP", $"{Name}: revoked device={deviceId} status={(int)cleanup.StatusCode}");
            }
        }

        authority?.Dispose();

        return Finish(transcript, cases,
            "Barkeep serves the LAN over TLS the client validated by name and chain, admits only paired devices, and revokes immediately.");
    }

    private static async Task<bool> SocketCasesAsync(
        Transcript transcript,
        HttpClient lan,
        Uri secure,
        X509Certificate2? authority)
    {
        using var issued = await lan.PostAsync("/api/live/ticket", Empty()).ConfigureAwait(false);
        var ticket = await issued.Content.ReadFromJsonAsync<TicketView>().ConfigureAwait(false);

        if (ticket is null)
        {
            return Case(transcript, "live-ticket", false, $"status={(int)issued.StatusCode}");
        }

        var socketUri = new Uri($"wss://{secure.Host}:{secure.Port}/ws/live?ticket={ticket.Ticket}");
        var first = await ConnectAsync(socketUri, authority).ConfigureAwait(false);
        var reused = await ConnectAsync(socketUri, authority).ConfigureAwait(false);

        var opened = Case(transcript, "live-socket-takes-a-ticket", first is not null, first ?? "refused");
        var spent = Case(transcript, "live-ticket-is-single-use", reused is null, reused ?? "refused as expected");

        // Reconnection after sleep: a second ticket, a second socket, state again.
        using var again = await lan.PostAsync("/api/live/ticket", Empty()).ConfigureAwait(false);
        var second = await again.Content.ReadFromJsonAsync<TicketView>().ConfigureAwait(false);
        var reconnected = second is null
            ? null
            : await ConnectAsync(new Uri($"wss://{secure.Host}:{secure.Port}/ws/live?ticket={second.Ticket}"), authority)
                .ConfigureAwait(false);

        var resumed = Case(transcript, "socket-reconnects-after-sleep", reconnected is not null, reconnected ?? "refused");

        return opened && spent && resumed;
    }

    private static async Task<string?> ConnectAsync(Uri uri, X509Certificate2? authority)
    {
        using var socket = new ClientWebSocket();
        socket.Options.RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
            Trusts(authority, certificate, errors);
        socket.Options.SetRequestHeader("Origin", $"https://{uri.Host}:{uri.Port}");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            await socket.ConnectAsync(uri, timeout.Token).ConfigureAwait(false);
            var buffer = new byte[16 * 1024];
            var frame = await socket.ReceiveAsync(buffer, timeout.Token).ConfigureAwait(false);
            using var state = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, frame.Count));
            return $"freshness={state.RootElement.GetProperty("freshness").GetString()} bytes={frame.Count}";
        }
        catch (Exception error) when (error is WebSocketException or OperationCanceledException or JsonException)
        {
            return null;
        }
    }

    private static async Task<bool> ReplayCaseAsync(Transcript transcript, HttpClient lan)
    {
        var commandId = $"selftest-{Guid.NewGuid():N}";
        var body =
            $$$"""{"commandId":"{{{commandId}}}","kind":"Add","entry":{"hash":"selftest","title":"SelfTest LAN probe","artist":"Cantina"}}""";

        using var first = await lan.PostAsync("/api/setlist/commands", Json(body)).ConfigureAwait(false);
        using var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync().ConfigureAwait(false));
        var before = await CountAsync(lan).ConfigureAwait(false);

        using var replay = await lan.PostAsync("/api/setlist/commands", Json(body)).ConfigureAwait(false);
        using var replayBody = JsonDocument.Parse(await replay.Content.ReadAsStringAsync().ConfigureAwait(false));
        var after = await CountAsync(lan).ConfigureAwait(false);

        var applied = !firstBody.RootElement.GetProperty("replayed").GetBoolean();
        var converged = replayBody.RootElement.GetProperty("replayed").GetBoolean();

        return Case(transcript, "reconnect-does-not-replay-a-command",
            applied && converged && before == after,
            $"first_applied={applied} second_replayed={converged} entries {before}->{after}");
    }

    private static async Task<int> CountAsync(HttpClient lan)
    {
        using var response = await lan.GetAsync("/api/setlist").ConfigureAwait(false);
        using var view = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        return view.RootElement.GetProperty("state").GetProperty("entries").GetArrayLength();
    }

    /// <summary>
    /// Trust for this theater and no other: the TLS stack's name check must have passed,
    /// and the chain must terminate in the certificate Barkeep served for onboarding. The
    /// machine's own root store takes no part in the decision.
    /// </summary>
    private static bool Trusts(X509Certificate2? authority, X509Certificate? presented, SslPolicyErrors errors)
    {
        if (presented is null ||
            errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable) ||
            errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            return false;
        }

        // No theater authority means the certificate should stand on its own against the
        // machine's root store, which is exactly what the iPad will do. Demanding less here
        // than the iPad demands would make this suite useless for the case it is testing.
        if (authority is null)
        {
            return errors == SslPolicyErrors.None;
        }

        using var leaf = X509CertificateLoader.LoadCertificate(presented.GetRawCertData());
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(authority);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(leaf);
    }

    private static bool Case(Transcript transcript, string name, bool pass, string detail)
    {
        transcript.Case(Name, name, pass, detail);
        return pass;
    }

    private static SuiteResult Finish(Transcript transcript, List<bool> cases, string claim)
    {
        var verdict = cases.Count > 0 && cases.All(passed => passed) ? Verdict.Pass : Verdict.Fail;
        transcript.Log("VERDICT", $"{Name}: {verdict} cases={cases.Count(passed => passed)}/{cases.Count}");
        return new SuiteResult(Name, verdict, claim);
    }

    private static StringContent Empty() => Json("{}");

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private sealed record OnboardingView(
        string Service,
        string TheaterName,
        string SecureUrl,
        string CertificateUrl,
        string CertificateFingerprint,
        bool NeedsDeviceTrust,
        bool Paired);

    private sealed record WindowView(string Code, DateTimeOffset ExpiresAt, int AttemptsRemaining);

    private sealed record GrantView(string DeviceId, string Label, string Token);

    private sealed record TicketView(string Ticket, DateTimeOffset ExpiresAt);
}
