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

export async function cueSong(song: IndexedSong): Promise<CueStatus> {
  const response = await call('/api/cue', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      commandId: crypto.randomUUID(),
      entry: entryFor(song),
      query: song.title,
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
