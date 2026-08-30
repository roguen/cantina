# The chart provider: Chorus Encore

Cantina's "Find new songs" section speaks to **Chorus Encore** (enchor.us), the community
search engine for rhythm-game charts, using the same two endpoints Encore's own desktop
client — Geomitron Bridge, GPL-3.0, same author — speaks. This document is the
independently-recorded contract D-030 requires before any provider integration: what the
wire is, what the terms are, and how Cantina behaves because of what they are not.

## The wire

| Operation | Request | Notes |
|---|---|---|
| Search | `POST https://api.enchor.us/search` with `{"search": "...", "page": 1, "per_page": N}` | Unauthenticated. The server applies defaults for every omitted filter. `per_page` is capped at 250 by the schema; Cantina asks for 25. |
| Download | `GET https://files.enchor.us/{md5}.sng` | Unauthenticated. `{md5}_novideo.sng` exists for charts with video backgrounds; Cantina downloads the standard file. |

The response schema is versioned nowhere except Bridge's own source
(`src-shared/search-api.ts` in Geomitron/Bridge). Cantina therefore parses **leniently**:
it reads only the fields it needs (`md5`, `name`, `artist`, `album`, `charter`, `year`,
`song_length`, `hasVideoBackground`), ignores everything else, and skips any record
missing an md5 or name. A schema change breaks the section visibly — the search reports
its failure by name — never the rest of the product.

**The md5 is the chart's identity, not the file's checksum.** The `_novideo` variant
serves different bytes under the same md5, so Cantina does not verify downloaded bytes
against it. What it verifies instead is that the bytes parse as an SNGPKG v1 header
before the file is published to the import pipeline.

## The terms, reviewed 2026-08-30

There are none to cite. The review looked for published API terms on enchor.us, in the
Bridge repository, and on the project's Patreon, and found none. What it did find shapes
the posture:

- **Encore is a donation-funded community service.** Its Patreon states the operator
  seeks no profit — "my only concern is covering its operating costs" — and names
  **server API and file hosting expenses** as the top cost.
- **The API is used openly and unauthenticated** by Bridge and other community tools.
  There is no key issuance, no sign-up, and no robots-style policy.
- **Explicit permission is therefore not obtainable from documents.** If the operator
  publishes terms or objects to third-party clients, this integration honors that
  immediately — which is why it can be disabled with one setting.

## How Cantina behaves because of that

| Behavior | Where |
|---|---|
| Identifies itself: `User-Agent: Cantina/1.0 (+https://github.com/roguen/cantina)` on every request, so Encore's operator can see, contact, or block Cantina by name | `Program.cs` HttpClient registration |
| Searches at a walking pace: a server-side cooldown between outbound searches, and searches happen only when a person taps Search — never as-you-type | `EncoreClient`, the client's explicit form |
| Downloads only what a person chose, **one at a time**, with a rolling ceiling of 30/hour | `EncoreDownloadCoordinator` |
| Never mirrors, crawls, or bulk-fetches; 25 results per search, page 1 only | `EncoreOptions.PerPage` |
| One kill switch: `Encore:Enabled=false` makes the whole surface answer 404 | `EncoreOptions` |

## Content use

Charts are user-provided and typically embed the recording they chart. Cantina downloads
them for **local personal play on the theater PC** — the same act, from the same source,
as using Bridge itself — and never redistributes, re-hosts, or commits them (the
repository excludes song content by standing rule). Attribution survives end to end: the
charter's name arrives in the search results, is shown on the iPad, and is written into
the downloaded file's name.

## The handoff

A download is not an import. The coordinator stages the file **outside** the acquisition
watch directory, validates the SNGPKG header, and only then moves it in under its final
name (`{Artist} - {Name} ({Charter}).sng`, Bridge's own convention). From that moment the
D-030 acquisition pipeline owns it — settling, journaling, indexing, YARG's rescan,
play-next — and the iPad's arrivals feed reports the outcome. There is no second import
path.
