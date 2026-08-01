# Glossary

These terms are canonical in code, configuration, and documentation.

## Setlist

An ordered collection of song references plus a cursor identifying the current song.
Songs before the cursor have been handled; songs after it remain. Queue is acceptable
as a generic UI verb, but it is not a second state model.

## Barkeep

The bridge application/process running on the theater PC. The executable and host
project use this name. In code and unqualified project prose, **bridge** refers to this
role, not to the external product below.

## Geomitron Bridge

The optional, separately installed GPL desktop application used to search for and
download rhythm-game charts. Always include **Geomitron** when the distinction from
Barkeep could be unclear. Its completed `.sng` output is an acquisition source, not an
authoritative setlist or control service.

## Acquisition job

The target representation of an idempotent request to obtain, validate, index, refresh,
and optionally add a specific provider chart to the setlist. Durability remains open
in issue #7. The initial manual Geomitron Bridge handoff begins at arrival detection;
it is not falsely reported as a programmatic download job.

## Play-next intent

A request to place an exact indexed song immediately after the active setlist cursor.
If fresh YARG state proves the game is idle, Barkeep may cue it immediately. It is not
permission to interrupt an active song later.

## Queued

The exact Cantina song reference has been inserted into Barkeep's authoritative
Setlist. It does not mean YARG has loaded or started the song.

## Cued

The selected YARG adapter has returned its bounded success condition for asking stock
YARG to load the exact song. The target-PC control spike must define that condition;
`cued` never means playback or scoring has begun merely because a command was sent.

## Ambiguous outcome

Barkeep knows an external operation was requested but cannot safely prove whether it
took effect, usually because of a crash or lost acknowledgement. Ambiguous refreshes
or cues are not retried automatically.

## YARG session

The running YARG process and the observable state associated with it. Code should use
names such as `YargSessionState` and `IYargController`.

## Rejected: Stage

`Stage` is not canonical because it conflicts with deployment stages, theater staging,
and YARG's own scene/state vocabulary. It may appear in ordinary prose only.
