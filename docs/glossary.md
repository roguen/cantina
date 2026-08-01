# Glossary

These terms are canonical in code, configuration, and documentation.

## Setlist

An ordered collection of song references plus a cursor identifying the current song.
Songs before the cursor have been handled; songs after it remain. Queue is acceptable
as a generic UI verb, but it is not a second state model.

## Barkeep

The bridge application/process running on the theater PC. The executable and host
project use this name.

## YARG session

The running YARG process and the observable state associated with it. Code should use
names such as `YargSessionState` and `IYargController`.

## Rejected: Stage

`Stage` is not canonical because it conflicts with deployment stages, theater staging,
and YARG's own scene/state vocabulary. It may appear in ordinary prose only.
