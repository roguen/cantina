# Library ambiguity analysis — issue [#4](https://github.com/roguen/cantina/issues/4)

Answers one of #4's questions without touching YARG: **is there a metadata key that can be
turned into a query selecting exactly one song?**

This matters because it bounds the answer before any UI is driven. A search box can only
match on metadata. If the metadata cannot distinguish two songs, no query string can select
between them, however good the search implementation is.

## Running it

```powershell
pwsh -File spikes/library-ambiguity/scan.ps1 -Root "C:\path\to\Songs"
```

Reads `song.ini` files directly. It does not read YARG's cache, modify anything, or emit
song content — only counts and the field values needed to explain a collision.

## Result on the theater PC, 2026-08-02

447 songs from an existing Clone Hero library.

| Measure | Value |
|---|---|
| Songs sharing title + artist with another song | 37 (8.3%) |
| Ambiguous groups | 18 |
| Groups still ambiguous after adding **charter** | 9 of 18 |
| Groups still ambiguous after adding **charter + source** | 9 of 18 |
| Songs with an empty `source` | **447 of 447** |
| Songs whose title or charter contains rich-text markup | **385 of 447 (86%)** |

The worst case is three copies of one song differing only by charter:

```
title='Reptilia'  artist='The Strokes'  charter='Harmonix'
title='Reptilia'  artist='The Strokes'  charter='Symph'
title='Reptilia'  artist='The Strokes'  charter='<color=#ffa500>Neversoft</color>'
```

Two conclusions:

1. **No available metadata combination is a unique key.** Half the ambiguous groups survive
   every field this library populates. `source` is empty everywhere, so it discriminates
   nothing.
2. **Rich-text markup is the norm, not an edge case.** `<color=#ffa500>Neversoft</color>` is
   a real charter value, and **385 of 447 songs (86%)** carry markup in their title or
   charter. Any index, search, or iPad display must handle or strip these tags, and a query
   built from a raw field would not match what a user sees on screen. Sorting and full-text
   search over raw values would also be wrong. This is a larger M2 concern than an M4 one,
   and it was not anticipated anywhere in the brief.

The only unique identifiers are the song folder path and the content hash YARG writes to
`currentSong.json`. Neither can be typed into a search field.

## What this does not prove

It does not test YARG's search behavior, sort stability, indexing latency, or what happens
when a query returns several results. Those remain open in #4 and need the target PC. This
result only establishes the ceiling: for at least 9 groups in this library, **no query can
be unambiguous**, so a metadata-driven control path cannot be made deterministic for them.
