// SPDX-License-Identifier: LGPL-3.0-or-later

// Thin fetch wrappers over Barkeep's surface. Every mutating call carries a
// client-generated command id, so a retry after a lost response replays from the
// journal instead of acting twice (D-023).

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
  const response = await fetch(`/api/songs?query=${encodeURIComponent(query)}&limit=50`)
  if (!response.ok) throw new Error(`search failed: ${response.status}`)
  return response.json() as Promise<SongSearchResponse>
}

export async function fetchSetlist(): Promise<SetlistView> {
  const response = await fetch('/api/setlist')
  if (!response.ok) throw new Error(`setlist failed: ${response.status}`)
  return response.json() as Promise<SetlistView>
}

export async function addToSetlist(song: IndexedSong): Promise<void> {
  const response = await fetch('/api/setlist/commands', {
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
  const response = await fetch('/api/cue', {
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

export async function currentCue(): Promise<CueStatus | null> {
  const response = await fetch('/api/cue/current')
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
