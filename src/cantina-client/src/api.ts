// SPDX-License-Identifier: LGPL-3.0-or-later

// Thin fetch wrappers over Barkeep's surface. Every mutating call carries a
// client-generated command id, so a retry after a lost response replays from the
// journal instead of acting twice (D-023), and every call carries the paired device's
// bearer token when there is one (D-026).
//
// `crypto.randomUUID` needs a secure context, which is exactly what Barkeep's LAN binding
// provides and why loopback HTTP still works: localhost is a secure context too.

import type { CertificateHealth } from './certificateNotice'
import { forgetToken, pairingRefusal, storedToken } from './pairing'

/// Thrown when Barkeep says this device is not paired. The caller shows the pairing
/// screen rather than an error, because "not paired" is a state, not a failure.
export class NotPairedError extends Error {}

async function call(path: string, init?: RequestInit): Promise<Response> {
  const token = storedToken()
  const headers = new Headers(init?.headers)

  if (token) headers.set('Authorization', `Bearer ${token}`)

  const response = await fetch(path, { ...init, headers })

  if (response.status === 401) {
    const detail = pairingRefusal(response.status, await response.clone().text())
    forgetToken()
    throw new NotPairedError(detail ?? 'This iPad is not paired with the theater PC.')
  }

  return response
}

// The instrument picture, in the diff_ vocabulary every source speaks: -1 is "not
// charted"; a vocals chart is what makes lyrics available.
export type SongInstruments = {
  guitar: number
  bass: number
  drums: number
  keys: number
  vocals: number
}

export type IndexedSong = {
  location: string
  title: string
  artist: string
  album: string
  genre: string
  year: string
  charter: string
  songLengthMilliseconds: number | null
  learnedHash: string | null
  instruments: SongInstruments
}

export type SongSearchResponse = {
  results: IndexedSong[]
  totalIndexed: number
  lastScan: { indexed: number; skipped: unknown[]; durationMilliseconds: number }
}

export type SetlistEntry = {
  hash: string
  title: string
  artist: string
  location?: string | null
}

export type SetlistView = {
  state: { entries: SetlistEntry[]; cursor: number }
  recoveredAmbiguous: unknown[]
  quarantinedFiles: string[]
}

export type CueStatus = {
  commandId: string
  state: 'refused' | 'replayed' | 'pending-players' | 'failed' | 'done'
  detail: string
  requested: SetlistEntry
  loaded: { title: string; artist: string; hash: string } | null
}

export async function searchSongs(query: string): Promise<SongSearchResponse> {
  const response = await call(`/api/songs?query=${encodeURIComponent(query)}&limit=50`)
  if (!response.ok) throw new Error(`search failed: ${response.status}`)
  return response.json() as Promise<SongSearchResponse>
}

export async function fetchSetlist(): Promise<SetlistView> {
  const response = await call('/api/setlist')
  if (!response.ok) throw new Error(`setlist failed: ${response.status}`)
  return response.json() as Promise<SetlistView>
}

export async function addToSetlist(song: IndexedSong): Promise<void> {
  const response = await call('/api/setlist/commands', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      commandId: crypto.randomUUID(),
      kind: 'Add',
      entry: entryFor(song),
    }),
  })
  if (!response.ok) throw new Error(`add failed: ${response.status}`)
}

export async function removeFromSetlist(index: number, location: string | null): Promise<void> {
  const response = await call('/api/setlist/commands', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      commandId: crypto.randomUUID(),
      kind: 'Remove',
      cursor: index,
      location,
    }),
  })
  if (!response.ok) throw new Error(`remove failed: ${response.status}`)
}

export async function cueSong(song: IndexedSong): Promise<CueStatus> {
  const response = await call('/api/cue', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      commandId: crypto.randomUUID(),
      entry: entryFor(song),
      // Title alone sent YARG's fuzzy search to the wrong song when titles collide
      // or rank oddly; the artist disambiguates, and the load read-back still judges.
      query: `${song.title} ${song.artist}`,
    }),
  })
  if (!response.ok) throw new Error(`cue failed: ${response.status}`)
  return response.json() as Promise<CueStatus>
}

// The socket cannot carry a header, so a paired device spends its token here for a
// ticket good for one connection and thirty seconds (D-026).
export async function liveTicket(): Promise<string> {
  const response = await call('/api/live/ticket', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: '{}',
  })
  if (!response.ok) throw new Error(`ticket failed: ${response.status}`)
  const issued = (await response.json()) as { ticket: string }
  return issued.ticket
}

// Barkeep's own view of the certificate it is serving, so a renewal that quietly stopped
// is visible on the iPad while the iPad can still connect at all (D-029).
export async function fetchHealth(): Promise<{ certificate: CertificateHealth | null }> {
  const response = await call('/api/health')
  if (!response.ok) throw new Error(`health failed: ${response.status}`)
  return response.json() as Promise<{ certificate: CertificateHealth | null }>
}

// The honest acquisition feed (D-030): what arrived through the Geomitron Bridge handoff
// and what became of each item, from the pipeline's own journal of outcomes.
export type AcquisitionRecord = {
  fileName: string
  idempotencyKey: string
  outcome: 'Completed' | 'Failed' | 'Ambiguous' | 'Canceled' | 'InProgress' | 'Conflict'
  failureCode: string | null
  finishedAt: string
}

export async function recentAcquisitions(): Promise<AcquisitionRecord[]> {
  const response = await call('/api/acquisition/recent')
  if (!response.ok) throw new Error(`acquisitions failed: ${response.status}`)
  return response.json() as Promise<AcquisitionRecord[]>
}

// Starred songs, stored on the theater so every paired device sees the same list.
export async function fetchFavorites(): Promise<string[]> {
  const response = await call('/api/favorites')
  if (!response.ok) throw new Error(`favorites failed: ${response.status}`)
  return response.json() as Promise<string[]>
}

export async function setFavorite(location: string, favored: boolean): Promise<string[]> {
  const response = await call('/api/favorites', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ location, favored }),
  })
  if (!response.ok) throw new Error(`favorite failed: ${response.status}`)
  return response.json() as Promise<string[]>
}

// The score screen's one key, pressed from the iPad. Refused unless the wire shows
// the score screen, so it can never land blind.
export async function scoreContinue(): Promise<{ state: string; detail: string }> {
  const response = await call('/api/score/continue', { method: 'POST' })
  if (!response.ok) throw new Error(`continue failed: ${response.status}`)
  return response.json() as Promise<{ state: string; detail: string }>
}

// The score-screen advance (#39): armed from the iPad, off at startup, honest about
// every episode in its detail sentence.
export type AdvanceStatus = {
  enabled: boolean
  phase: string
  detail: string
  updatedAt: string
}

export async function advanceStatus(): Promise<AdvanceStatus> {
  const response = await call('/api/advance')
  if (!response.ok) throw new Error(`advance status failed: ${response.status}`)
  return response.json() as Promise<AdvanceStatus>
}

export async function setAdvance(enabled: boolean): Promise<AdvanceStatus> {
  const response = await call('/api/advance', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ enabled }),
  })
  if (!response.ok) throw new Error(`advance toggle failed: ${response.status}`)
  return response.json() as Promise<AdvanceStatus>
}

// The chart-provider surface (D-032): search Chorus Encore and hand a chosen chart to
// the acquisition pipeline. 404 means the integration is off and the section is not drawn.
export type EncoreChart = {
  md5: string
  name: string
  artist: string
  inLibrary: boolean
  album: string | null
  charter: string | null
  year: string | null
  songLengthMilliseconds: number
  hasVideoBackground: boolean
  instruments: SongInstruments
}

export type EncoreSearchResult = {
  found: number
  charts: EncoreChart[]
  refusal: string | null
}

export type ProviderDownload = {
  md5: string
  title: string
  artist: string
  state: 'downloading' | 'delivered' | 'failed' | 'refused'
  detail: string
  startedAt: string
}

export async function providerEnabled(): Promise<boolean> {
  const response = await call('/api/provider')
  return response.ok
}

export async function providerSearch(q: string): Promise<EncoreSearchResult> {
  const response = await call(`/api/provider/search?q=${encodeURIComponent(q)}`)
  if (!response.ok) throw new Error(`provider search failed: ${response.status}`)
  return response.json() as Promise<EncoreSearchResult>
}

export async function providerDownload(chart: EncoreChart): Promise<ProviderDownload> {
  const response = await call('/api/provider/download', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(chart),
  })
  if (!response.ok) throw new Error(`provider download failed: ${response.status}`)
  return response.json() as Promise<ProviderDownload>
}

export async function providerDownloads(): Promise<ProviderDownload[]> {
  const response = await call('/api/provider/downloads')
  if (!response.ok) throw new Error(`provider downloads failed: ${response.status}`)
  return response.json() as Promise<ProviderDownload[]>
}

// The debug surface (config-gated, Debug:Enabled). 404 means it is off, which the
// client treats as "draw nothing" — the section only exists on a bench.
export type DebugView = { enabled: boolean; playerConfirmations: number }

export type StandInStatus = { state: string; detail: string }

export async function debugView(): Promise<DebugView | null> {
  const response = await call('/api/debug')
  if (response.status === 404) return null
  if (!response.ok) throw new Error(`debug view failed: ${response.status}`)
  return response.json() as Promise<DebugView>
}

export async function standInForPlayers(): Promise<StandInStatus> {
  const response = await call('/api/debug/players', { method: 'POST' })
  if (!response.ok) throw new Error(`stand-in failed: ${response.status}`)
  return response.json() as Promise<StandInStatus>
}

// The name this device was registered under - "iPad Mini" on the iPad Mini - from
// the server's registry, so the masthead says who this screen is.
export async function deviceLabel(): Promise<string | null> {
  const response = await call('/api/device')
  if (!response.ok) return null
  const view = (await response.json()) as { label: string }
  return view.label
}

export async function currentCue(): Promise<CueStatus | null> {
  const response = await call('/api/cue/current')
  if (response.status === 204) return null
  if (!response.ok) throw new Error(`cue status failed: ${response.status}`)
  return response.json() as Promise<CueStatus>
}

function entryFor(song: IndexedSong): SetlistEntry {
  return {
    hash: song.learnedHash ?? '',
    title: song.title,
    artist: song.artist,
    location: song.location,
  }
}

// YARG charter fields carry inline color tags (D-025 keeps index data raw); the display
// strips them.
export function stripColorTags(value: string): string {
  return value.replace(/<\/?color[^>]*>/g, '')
}
