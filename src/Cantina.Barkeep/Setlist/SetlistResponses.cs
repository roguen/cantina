// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Setlist;

/// <summary>
/// The setlist as the client reads it: state plus the honesty annexes — intents recovered
/// as ambiguous awaiting confirmation, and any quarantined files behind a "state was
/// recovered" notice (D-023).
/// </summary>
public sealed record SetlistView(
    SetlistState State,
    IReadOnlyList<SetlistIntent> RecoveredAmbiguous,
    IReadOnlyList<string> QuarantinedFiles);

public sealed record CommandReceipt(string CommandId, SetlistOutcome Outcome, bool Replayed);

public sealed record CommandRejected(string Reason);
