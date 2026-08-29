// SPDX-License-Identifier: LGPL-3.0-or-later

// Mirrors Barkeep's LiveState projection (docs/live-state.md). The client renders what
// Barkeep observed and never invents: a stale or dead feed is said out loud, a fault is
// shown by name, and nothing here guesses position or progress.

export type LiveScene =
  | 'Unknown'
  | 'Menu'
  | 'Gameplay'
  | 'Score'
  | 'Calibration'
  | 'Practice'

export type LivePlayState = 'NoSong' | 'Playing' | 'Paused'

export type LiveFreshness = 'Live' | 'Stale' | 'Dead'

export type SessionFault =
  | 'None'
  | 'NoDatagrams'
  | 'StreamDead'
  | 'MultipleSources'
  | 'PortConflict'

export type LatchedSong = {
  title: string
  artist: string
  hash: string
  location: string
}

export type LiveState = {
  scene: LiveScene
  playState: LivePlayState
  song: LatchedSong | null
  songSource: 'Unknown' | 'Observed' | 'CuedByBarkeep'
  receivedAt: string | null
  freshness: LiveFreshness
  fault: SessionFault
  senders: string[]
  datagramsAccepted: number
  datagramsRejected: number
}

export type StageCopy = {
  headline: string
  detail: string
  tone: 'live' | 'attention' | 'down'
}

// The iPad's words for each honest state, per docs/failure-behavior.md: what stands in
// the way, and who can fix it.
export function stageCopy(state: LiveState): StageCopy {
  if (state.fault === 'PortConflict') {
    return {
      headline: 'Another app holds the YARG data port',
      detail: 'Barkeep cannot listen until it is closed.',
      tone: 'down',
    }
  }

  if (state.fault === 'MultipleSources') {
    return {
      headline: 'Two YARG instances are broadcasting',
      detail: 'Close one; Cantina will not guess which game is real.',
      tone: 'down',
    }
  }

  if (state.freshness === 'Dead') {
    return {
      headline: 'YARG is not observable',
      detail:
        state.fault === 'NoDatagrams'
          ? 'No data has arrived. Is YARG running with its UDP stream enabled?'
          : 'The stream stopped. YARG may have exited.',
      tone: 'down',
    }
  }

  const dimmed = state.freshness === 'Stale' ? ' (last known — the feed is stale)' : ''

  if (state.scene === 'Gameplay' && state.song) {
    const verb = state.playState === 'Paused' ? 'Paused' : 'Now playing'
    return {
      headline: `${verb}: ${state.song.title}`,
      detail: `${state.song.artist}${dimmed}`,
      tone: state.playState === 'Paused' ? 'attention' : 'live',
    }
  }

  if (state.scene === 'Score') {
    return {
      headline: 'On the score screen',
      detail: `Players decide what happens next${dimmed}`,
      tone: 'attention',
    }
  }

  return {
    headline: 'At the menus',
    detail: `YARG is between songs${dimmed}`,
    tone: 'live',
  }
}
