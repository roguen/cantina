// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Acquisition;

namespace Cantina.TestHarness;

public static class ScenarioCatalog
{
    private static readonly DateTimeOffset Baseline =
        new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static readonly SongArrivalCandidate Candidate =
        new("geomitron-bridge-handoff", "GeomitronBridge-001.sng", "harness-song-001");

    private static readonly SongIdentity Song = new("song-001");

    private static readonly ImportPlayNextRequest Request =
        new("intent-001", Candidate);

    private static readonly IReadOnlyList<ScenarioDefinition> Definitions =
    [
        new("idle-success", RunIdleSuccessAsync),
        new("playing-defers", RunPlayingDefersAsync),
        new("duplicate-idempotent", RunDuplicateIdempotentAsync),
        new("concurrent-same-key", RunConcurrentSameKeyAsync),
        new("idempotency-conflict", RunIdempotencyConflictAsync),
        new("arrival-rejected", RunArrivalRejectedAsync),
        new("song-not-visible", RunSongNotVisibleAsync),
        new("stale-idle-defers", RunStaleIdleDefersAsync),
        new("refresh-ambiguous", RunRefreshAmbiguousAsync),
        new("cue-ambiguous", RunCueAmbiguousAsync),
        new("cancel-before-dispatch", RunCancelBeforeDispatchAsync),
        new("adapter-throws-before-dispatch", RunAdapterThrowsBeforeDispatchAsync),
        new("refresh-canceled-after-dispatch", RunRefreshCanceledAfterDispatchAsync),
        new("cue-throws-after-dispatch", RunCueThrowsAfterDispatchAsync),
    ];

    public static IReadOnlyList<string> Names { get; } =
        Definitions.Select(definition => definition.Name).ToArray();

    public static bool Contains(string name) =>
        Definitions.Any(definition =>
            string.Equals(definition.Name, name, StringComparison.Ordinal));

    public static async Task<HarnessScenarioResult> RunAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var definition = Definitions.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.Ordinal));
        if (definition is null)
        {
            throw new ArgumentException("Unknown harness scenario.", nameof(name));
        }

        try
        {
            return await definition.Run(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new HarnessScenarioResult(
                name,
                false,
                "exception",
                [],
                [],
                ["scenario-exception"]);
        }
    }

    public static async Task<IReadOnlyList<HarnessScenarioResult>> RunAllAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<HarnessScenarioResult>(Definitions.Count);
        foreach (var definition in Definitions)
        {
            results.Add(await RunAsync(definition.Name, cancellationToken));
        }

        return results.AsReadOnly();
    }

    private static async Task<HarnessScenarioResult> RunIdleSuccessAsync(
        CancellationToken cancellationToken)
    {
        var world = CreateWorld(
            [SongArrivalProbeResult.Stabilizing(), SongArrivalProbeResult.Ready()],
            SongIndexResult.Accepted(Song),
            [Fresh(YargActivity.Idle, true), Fresh(YargActivity.Idle, false)]);

        var result = await world.Coordinator.RunAsync(Request, cancellationToken);

        return Evaluate(
            "idle-success",
            [result],
            world.Calls,
            ["completed"],
            [
                "1:detected",
                "1:stabilizing",
                "1:validating",
                "1:indexed:song-001",
                "1:refresh-pending",
                "1:yarg-visible:song-001",
                "1:queued:song-001",
                "1:cued:song-001",
            ],
            [
                "arrival.probe:stabilizing",
                "arrival.probe:ready",
                "song.validate-index:GeomitronBridge-001.sng",
                "yarg.observe:idle",
                "yarg.refresh:song-001",
                "yarg.resolve:song-001",
                "setlist.insert-next:song-001",
                "yarg.observe:idle",
                "yarg.cue:song-001",
            ]);
    }

    private static async Task<HarnessScenarioResult> RunPlayingDefersAsync(
        CancellationToken cancellationToken)
    {
        var world = CreateWorld(
            [SongArrivalProbeResult.Ready()],
            SongIndexResult.Accepted(Song),
            [
                Fresh(YargActivity.Active, false),
                Fresh(YargActivity.Idle, true),
                Fresh(YargActivity.Idle, false),
            ]);

        var result = await world.Coordinator.RunAsync(Request, cancellationToken);

        return Evaluate(
            "playing-defers",
            [result],
            world.Calls,
            ["completed"],
            [
                "1:detected",
                "1:validating",
                "1:indexed:song-001",
                "1:refresh-pending",
                "1:yarg-visible:song-001",
                "1:queued:song-001",
                "1:cued:song-001",
            ],
            [
                "arrival.probe:ready",
                "song.validate-index:GeomitronBridge-001.sng",
                "yarg.observe:active",
                "yarg.observe:idle",
                "yarg.refresh:song-001",
                "yarg.resolve:song-001",
                "setlist.insert-next:song-001",
                "yarg.observe:idle",
                "yarg.cue:song-001",
            ]);
    }

    private static async Task<HarnessScenarioResult> RunDuplicateIdempotentAsync(
        CancellationToken cancellationToken)
    {
        var world = CreateWorld(
            [SongArrivalProbeResult.Ready()],
            SongIndexResult.Accepted(Song),
            [Fresh(YargActivity.Idle, true), Fresh(YargActivity.Idle, false)]);

        var first = await world.Coordinator.RunAsync(Request, cancellationToken);
        var duplicate = await world.Coordinator.RunAsync(Request, cancellationToken);

        return Evaluate(
            "duplicate-idempotent",
            [first, duplicate],
            world.Calls,
            ["completed", "completed"],
            [
                "1:detected",
                "1:validating",
                "1:indexed:song-001",
                "1:refresh-pending",
                "1:yarg-visible:song-001",
                "1:queued:song-001",
                "1:cued:song-001",
                "2:terminal-replay",
            ],
            [
                "arrival.probe:ready",
                "song.validate-index:GeomitronBridge-001.sng",
                "yarg.observe:idle",
                "yarg.refresh:song-001",
                "yarg.resolve:song-001",
                "setlist.insert-next:song-001",
                "yarg.observe:idle",
                "yarg.cue:song-001",
            ]);
    }

    private static async Task<HarnessScenarioResult> RunConcurrentSameKeyAsync(
        CancellationToken cancellationToken)
    {
        var calls = new HarnessCallLog();
        var arrival = new CoordinatedSongArrivalPort(calls);
        var journal = new InMemoryImportPlayNextJournal();
        var coordinator = new ImportPlayNextCoordinator(
            arrival,
            new ScriptedSongIndexPort(SongIndexResult.Accepted(Song), calls),
            new ScriptedYargSessionPort(
                [Fresh(YargActivity.Idle, true), Fresh(YargActivity.Idle, false)],
                ExternalCommandOutcome.Succeeded,
                true,
                ExternalCommandOutcome.Succeeded,
                calls),
            new InMemorySetlistPort(calls),
            journal,
            new ManualHarnessTimeProvider(Baseline));

        var firstRun = coordinator.RunAsync(Request, cancellationToken).AsTask();
        var claimOrCompletion = await Task.WhenAny(journal.FirstClaimAcquired, firstRun);
        if (claimOrCompletion == firstRun)
        {
            return ScenarioFailure(
                "concurrent-same-key",
                "first-run-completed-before-claim",
                calls);
        }

        var entryOrCompletion = await Task.WhenAny(arrival.FirstEntry, firstRun);
        if (entryOrCompletion == firstRun)
        {
            return ScenarioFailure(
                "concurrent-same-key",
                "first-run-completed-before-arrival-entry",
                calls);
        }

        var callsBeforeConcurrentRun = calls.Count;
        var concurrentRun = coordinator.RunAsync(Request, cancellationToken).AsTask();
        var completionOrPortEntry = await Task.WhenAny(
            concurrentRun,
            arrival.UnexpectedEntry);
        var concurrentReachedWorkflowPort = completionOrPortEntry == arrival.UnexpectedEntry;
        var concurrent = await concurrentRun;
        var concurrentHadNoSideEffects =
            !concurrentReachedWorkflowPort && calls.Count == callsBeforeConcurrentRun;
        arrival.ReleaseFirst();
        var first = await firstRun;

        return Evaluate(
            "concurrent-same-key",
            [first, concurrent],
            calls,
            ["completed", "in-progress"],
            [
                "1:detected",
                "1:validating",
                "1:indexed:song-001",
                "1:refresh-pending",
                "1:yarg-visible:song-001",
                "1:queued:song-001",
                "1:cued:song-001",
                "2:in-progress",
            ],
            [
                "arrival.probe:blocked",
                "song.validate-index:GeomitronBridge-001.sng",
                "yarg.observe:idle",
                "yarg.refresh:song-001",
                "yarg.resolve:song-001",
                "setlist.insert-next:song-001",
                "yarg.observe:idle",
                "yarg.cue:song-001",
            ],
            concurrentHadNoSideEffects
                ? []
                : ["concurrent claimant performed workflow side effects"]);
    }

    private static async Task<HarnessScenarioResult> RunIdempotencyConflictAsync(
        CancellationToken cancellationToken)
    {
        var world = CreateWorld(
            [SongArrivalProbeResult.Ready()],
            SongIndexResult.Accepted(Song),
            [Fresh(YargActivity.Idle, true), Fresh(YargActivity.Idle, false)]);
        var conflictingRequest = new ImportPlayNextRequest(
            Request.IdempotencyKey,
            Candidate with { RelativePath = "GeomitronBridge-002.sng" });

        var first = await world.Coordinator.RunAsync(Request, cancellationToken);
        var conflict = await world.Coordinator.RunAsync(
            conflictingRequest,
            cancellationToken);

        return Evaluate(
            "idempotency-conflict",
            [first, conflict],
            world.Calls,
            ["completed", "conflict"],
            [
                "1:detected",
                "1:validating",
                "1:indexed:song-001",
                "1:refresh-pending",
                "1:yarg-visible:song-001",
                "1:queued:song-001",
                "1:cued:song-001",
                "2:idempotency-conflict:idempotency-key-reused",
            ],
            [
                "arrival.probe:ready",
                "song.validate-index:GeomitronBridge-001.sng",
                "yarg.observe:idle",
                "yarg.refresh:song-001",
                "yarg.resolve:song-001",
                "setlist.insert-next:song-001",
                "yarg.observe:idle",
                "yarg.cue:song-001",
            ]);
    }

    private static async Task<HarnessScenarioResult> RunArrivalRejectedAsync(
        CancellationToken cancellationToken)
    {
        var world = CreateWorld(
            [SongArrivalProbeResult.Rejected("path-outside-root")],
            SongIndexResult.Rejected("not-used"),
            []);

        var result = await world.Coordinator.RunAsync(Request, cancellationToken);
        var replay = await world.Coordinator.RunAsync(Request, cancellationToken);

        return Evaluate(
            "arrival-rejected",
            [result, replay],
            world.Calls,
            ["failed", "failed"],
            [
                "1:detected",
                "1:failed:path-outside-root",
                "2:terminal-replay",
            ],
            ["arrival.probe:rejected"]);
    }

    private static async Task<HarnessScenarioResult> RunSongNotVisibleAsync(
        CancellationToken cancellationToken)
    {
        var world = CreateWorld(
            [SongArrivalProbeResult.Ready()],
            SongIndexResult.Accepted(Song),
            [Fresh(YargActivity.Idle, true)],
            songBecomesVisible: false);

        var result = await world.Coordinator.RunAsync(Request, cancellationToken);

        return Evaluate(
            "song-not-visible",
            [result],
            world.Calls,
            ["failed"],
            [
                "1:detected",
                "1:validating",
                "1:indexed:song-001",
                "1:refresh-pending",
                "1:failed:song-not-visible",
            ],
            [
                "arrival.probe:ready",
                "song.validate-index:GeomitronBridge-001.sng",
                "yarg.observe:idle",
                "yarg.refresh:song-001",
                "yarg.resolve:song-001",
            ]);
    }

    private static async Task<HarnessScenarioResult> RunStaleIdleDefersAsync(
        CancellationToken cancellationToken)
    {
        var world = CreateWorld(
            [SongArrivalProbeResult.Ready()],
            SongIndexResult.Accepted(Song),
            [
                new YargSessionSnapshot(
                    YargActivity.Idle,
                    Baseline - TimeSpan.FromSeconds(30),
                    true),
                Fresh(YargActivity.Idle, true),
                Fresh(YargActivity.Idle, false),
            ]);

        var result = await world.Coordinator.RunAsync(Request, cancellationToken);

        return Evaluate(
            "stale-idle-defers",
            [result],
            world.Calls,
            ["completed"],
            [
                "1:detected",
                "1:validating",
                "1:indexed:song-001",
                "1:refresh-pending",
                "1:yarg-visible:song-001",
                "1:queued:song-001",
                "1:cued:song-001",
            ],
            [
                "arrival.probe:ready",
                "song.validate-index:GeomitronBridge-001.sng",
                "yarg.observe:idle",
                "yarg.observe:idle",
                "yarg.refresh:song-001",
                "yarg.resolve:song-001",
                "setlist.insert-next:song-001",
                "yarg.observe:idle",
                "yarg.cue:song-001",
            ]);
    }

    private static async Task<HarnessScenarioResult> RunCueAmbiguousAsync(
        CancellationToken cancellationToken)
    {
        var world = CreateWorld(
            [SongArrivalProbeResult.Ready()],
            SongIndexResult.Accepted(Song),
            [Fresh(YargActivity.Idle, true), Fresh(YargActivity.Idle, false)],
            cueOutcome: ExternalCommandOutcome.Ambiguous);

        var first = await world.Coordinator.RunAsync(Request, cancellationToken);
        var duplicate = await world.Coordinator.RunAsync(Request, cancellationToken);

        return Evaluate(
            "cue-ambiguous",
            [first, duplicate],
            world.Calls,
            ["ambiguous", "ambiguous"],
            [
                "1:detected",
                "1:validating",
                "1:indexed:song-001",
                "1:refresh-pending",
                "1:yarg-visible:song-001",
                "1:queued:song-001",
                "1:cue-ambiguous:cue-outcome-unknown",
                "2:terminal-replay",
            ],
            [
                "arrival.probe:ready",
                "song.validate-index:GeomitronBridge-001.sng",
                "yarg.observe:idle",
                "yarg.refresh:song-001",
                "yarg.resolve:song-001",
                "setlist.insert-next:song-001",
                "yarg.observe:idle",
                "yarg.cue:song-001",
            ]);
    }

    private static async Task<HarnessScenarioResult> RunRefreshAmbiguousAsync(
        CancellationToken cancellationToken)
    {
        var world = CreateWorld(
            [SongArrivalProbeResult.Ready()],
            SongIndexResult.Accepted(Song),
            [Fresh(YargActivity.Idle, true)],
            refreshOutcome: ExternalCommandOutcome.Ambiguous);

        var first = await world.Coordinator.RunAsync(Request, cancellationToken);
        var duplicate = await world.Coordinator.RunAsync(Request, cancellationToken);

        return Evaluate(
            "refresh-ambiguous",
            [first, duplicate],
            world.Calls,
            ["ambiguous", "ambiguous"],
            [
                "1:detected",
                "1:validating",
                "1:indexed:song-001",
                "1:refresh-pending",
                "1:refresh-ambiguous:refresh-outcome-unknown",
                "2:terminal-replay",
            ],
            [
                "arrival.probe:ready",
                "song.validate-index:GeomitronBridge-001.sng",
                "yarg.observe:idle",
                "yarg.refresh:song-001",
            ]);
    }

    private static async Task<HarnessScenarioResult> RunCancelBeforeDispatchAsync(
        CancellationToken cancellationToken)
    {
        using var callerCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var calls = new HarnessCallLog();
        var journal = new InMemoryImportPlayNextJournal();
        var coordinator = new ImportPlayNextCoordinator(
            new CancelingSongArrivalPort(calls, callerCancellation),
            new ScriptedSongIndexPort(SongIndexResult.Accepted(Song), calls),
            new ScriptedYargSessionPort(
                [],
                ExternalCommandOutcome.Succeeded,
                true,
                ExternalCommandOutcome.Succeeded,
                calls),
            new InMemorySetlistPort(calls),
            journal,
            new ManualHarnessTimeProvider(Baseline));

        var canceled = await coordinator.RunAsync(Request, callerCancellation.Token);
        var replay = await coordinator.RunAsync(Request, cancellationToken);

        return Evaluate(
            "cancel-before-dispatch",
            [canceled, replay],
            calls,
            ["canceled", "canceled"],
            [
                "1:detected",
                "1:canceled:operation-canceled-before-dispatch",
                "2:terminal-replay",
            ],
            ["arrival.probe:cancel"]);
    }

    private static async Task<HarnessScenarioResult> RunRefreshCanceledAfterDispatchAsync(
        CancellationToken cancellationToken)
    {
        var world = CreateWorld(
            [SongArrivalProbeResult.Ready()],
            SongIndexResult.Accepted(Song),
            [Fresh(YargActivity.Idle, true)],
            refreshFault: ScriptedCommandFault.CancelAfterDispatch);

        var canceled = await world.Coordinator.RunAsync(Request, cancellationToken);
        var replay = await world.Coordinator.RunAsync(Request, cancellationToken);

        return Evaluate(
            "refresh-canceled-after-dispatch",
            [canceled, replay],
            world.Calls,
            ["ambiguous", "ambiguous"],
            [
                "1:detected",
                "1:validating",
                "1:indexed:song-001",
                "1:refresh-pending",
                "1:refresh-ambiguous:refresh-dispatch-canceled",
                "2:terminal-replay",
            ],
            [
                "arrival.probe:ready",
                "song.validate-index:GeomitronBridge-001.sng",
                "yarg.observe:idle",
                "yarg.refresh:song-001",
            ]);
    }

    private static async Task<HarnessScenarioResult> RunAdapterThrowsBeforeDispatchAsync(
        CancellationToken cancellationToken)
    {
        var calls = new HarnessCallLog();
        var journal = new InMemoryImportPlayNextJournal();
        var coordinator = new ImportPlayNextCoordinator(
            new ThrowingSongArrivalPort(calls),
            new ScriptedSongIndexPort(SongIndexResult.Accepted(Song), calls),
            new ScriptedYargSessionPort(
                [],
                ExternalCommandOutcome.Succeeded,
                true,
                ExternalCommandOutcome.Succeeded,
                calls),
            new InMemorySetlistPort(calls),
            journal,
            new ManualHarnessTimeProvider(Baseline));

        var failed = await coordinator.RunAsync(Request, cancellationToken);
        var replay = await coordinator.RunAsync(Request, cancellationToken);

        return Evaluate(
            "adapter-throws-before-dispatch",
            [failed, replay],
            calls,
            ["failed", "failed"],
            [
                "1:detected",
                "1:failed:adapter-error-before-dispatch",
                "2:terminal-replay",
            ],
            ["arrival.probe:throw"]);
    }

    private static async Task<HarnessScenarioResult> RunCueThrowsAfterDispatchAsync(
        CancellationToken cancellationToken)
    {
        var world = CreateWorld(
            [SongArrivalProbeResult.Ready()],
            SongIndexResult.Accepted(Song),
            [Fresh(YargActivity.Idle, true), Fresh(YargActivity.Idle, false)],
            cueFault: ScriptedCommandFault.ThrowAfterDispatch);

        var failed = await world.Coordinator.RunAsync(Request, cancellationToken);
        var replay = await world.Coordinator.RunAsync(Request, cancellationToken);

        return Evaluate(
            "cue-throws-after-dispatch",
            [failed, replay],
            world.Calls,
            ["ambiguous", "ambiguous"],
            [
                "1:detected",
                "1:validating",
                "1:indexed:song-001",
                "1:refresh-pending",
                "1:yarg-visible:song-001",
                "1:queued:song-001",
                "1:cue-ambiguous:cue-dispatch-threw",
                "2:terminal-replay",
            ],
            [
                "arrival.probe:ready",
                "song.validate-index:GeomitronBridge-001.sng",
                "yarg.observe:idle",
                "yarg.refresh:song-001",
                "yarg.resolve:song-001",
                "setlist.insert-next:song-001",
                "yarg.observe:idle",
                "yarg.cue:song-001",
            ]);
    }

    private static HarnessWorld CreateWorld(
        IEnumerable<SongArrivalProbeResult> probes,
        SongIndexResult indexResult,
        IEnumerable<YargSessionSnapshot> snapshots,
        ExternalCommandOutcome refreshOutcome = ExternalCommandOutcome.Succeeded,
        bool songBecomesVisible = true,
        ExternalCommandOutcome cueOutcome = ExternalCommandOutcome.Succeeded,
        ScriptedCommandFault refreshFault = ScriptedCommandFault.None,
        ScriptedCommandFault cueFault = ScriptedCommandFault.None)
    {
        var calls = new HarnessCallLog();
        var clock = new ManualHarnessTimeProvider(Baseline);
        var journal = new InMemoryImportPlayNextJournal();
        var coordinator = new ImportPlayNextCoordinator(
            new ScriptedSongArrivalPort(probes, calls),
            new ScriptedSongIndexPort(indexResult, calls),
            new ScriptedYargSessionPort(
                snapshots,
                refreshOutcome,
                songBecomesVisible,
                cueOutcome,
                calls,
                refreshFault,
                cueFault),
            new InMemorySetlistPort(calls),
            journal,
            clock);

        return new HarnessWorld(coordinator, calls);
    }

    private static HarnessScenarioResult Evaluate(
        string name,
        IReadOnlyList<ImportPlayNextResult> runs,
        HarnessCallLog calls,
        IReadOnlyList<string> expectedOutcomes,
        IReadOnlyList<string> expectedEvents,
        IReadOnlyList<string> expectedCalls,
        IReadOnlyList<string>? invariantFailures = null)
    {
        var events = runs
            .SelectMany((run, index) => run.Events.Select(workflowEvent =>
                new HarnessEventReport(
                    index + 1,
                    workflowEvent.Sequence,
                    HarnessNames.State(workflowEvent.State),
                    workflowEvent.Detail)))
            .ToArray();

        var outcomes = runs.Select(run => HarnessNames.Outcome(run.Outcome)).ToArray();
        var eventLabels = events.Select(EventLabel).ToArray();
        var failures = new List<string>();

        Compare("outcomes", outcomes, expectedOutcomes, failures);
        Compare("events", eventLabels, expectedEvents, failures);
        var callSnapshot = calls.Snapshot();
        Compare("calls", callSnapshot, expectedCalls, failures);
        if (invariantFailures is not null)
        {
            failures.AddRange(invariantFailures);
        }

        return new HarnessScenarioResult(
            name,
            failures.Count == 0,
            string.Join(" + ", outcomes),
            events,
            callSnapshot,
            failures.AsReadOnly());
    }

    private static string EventLabel(HarnessEventReport workflowEvent) =>
        workflowEvent.Detail is null
            ? $"{workflowEvent.Run}:{workflowEvent.State}"
            : $"{workflowEvent.Run}:{workflowEvent.State}:{workflowEvent.Detail}";

    private static void Compare(
        string label,
        IEnumerable<string> actual,
        IEnumerable<string> expected,
        List<string> failures)
    {
        var actualValues = actual.ToArray();
        var expectedValues = expected.ToArray();
        if (!actualValues.SequenceEqual(expectedValues, StringComparer.Ordinal))
        {
            failures.Add(
                $"{label}: expected [{string.Join(", ", expectedValues)}] " +
                $"but received [{string.Join(", ", actualValues)}]");
        }
    }

    private static YargSessionSnapshot Fresh(
        YargActivity activity,
        bool canRefreshLibrary) =>
        new(activity, Baseline, canRefreshLibrary);

    private static HarnessScenarioResult ScenarioFailure(
        string name,
        string failureCode,
        HarnessCallLog calls) =>
        new(
            name,
            false,
            "scenario-failed",
            [],
            calls.Snapshot(),
            [failureCode]);

    private sealed record ScenarioDefinition(
        string Name,
        Func<CancellationToken, Task<HarnessScenarioResult>> Run);

    private sealed record HarnessWorld(
        ImportPlayNextCoordinator Coordinator,
        HarnessCallLog Calls);
}
