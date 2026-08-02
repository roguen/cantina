# Glossary

These terms are canonical in code, configuration, and documentation.

## Setlist

An ordered collection of song references plus a cursor identifying the current song.
Songs before the cursor have been handled; songs after it remain. Queue is acceptable
as a generic UI verb, but it is not a second state model.

## Barkeep

The Cantina server application/process running on the theater PC. The executable and
host project use this name. Barkeep is never called "the bridge"; see the rejected term
below.

## Geomitron Bridge

The optional, separately installed GPL-3.0 desktop application used to search for and
download rhythm-game charts, maintained by an independent open-source project at
<https://github.com/Geomitron/Bridge>. Always written in full. The code identifier stem
is `GeomitronBridge` and the configuration key stem is `geomitronBridge`. Its completed
`.sng` output is an acquisition source, not an authoritative setlist or control service.

## Chart acquisition

The neutral role Geomitron Bridge fills: obtaining a chart from outside the theater and
landing it where Barkeep can index it. Use this phrase for the role, and the product
name only for the product. The replaceable interfaces are the chart catalog and the
chart acquirer.

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
and YARG's own scene/state vocabulary — including the Stage Kit, a real lighting device
in this exact problem domain. It may appear in ordinary prose only.

## Rejected: bridge as a role word

`bridge` does not describe Barkeep. It collided with Geomitron Bridge in the README, the
architecture spec, and harness fixture names before D-009 retired it. Barkeep is the
Cantina server. Bare `Bridge` is acceptable only inside verbatim upstream URLs, file
paths, and release titles.
