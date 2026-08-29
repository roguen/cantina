// SPDX-License-Identifier: LGPL-3.0-or-later

import { describe, expect, it } from 'vitest'
import { stageCopy, type LiveState } from './liveState'

const base: LiveState = {
  scene: 'Menu',
  playState: 'NoSong',
  song: null,
  songSource: 'Unknown',
  receivedAt: null,
  freshness: 'Live',
  fault: 'None',
  senders: [],
  datagramsAccepted: 0,
  datagramsRejected: 0,
}

describe('stageCopy', () => {
  it('names a dead stream instead of pretending', () => {
    const copy = stageCopy({ ...base, freshness: 'Dead', fault: 'NoDatagrams' })
    expect(copy.tone).toBe('down')
    expect(copy.headline).toContain('not observable')
  })

  it('names the port conflict fault (D-013)', () => {
    const copy = stageCopy({ ...base, fault: 'PortConflict' })
    expect(copy.headline).toContain('data port')
  })

  it('marks a stale feed as last-known rather than current', () => {
    const copy = stageCopy({
      ...base,
      freshness: 'Stale',
      scene: 'Gameplay',
      playState: 'Playing',
      song: { title: 'The Unforgiven', artist: 'Metallica', hash: 'h', location: 'l' },
    })
    expect(copy.detail).toContain('stale')
  })

  it('leaves the score screen to the players (D-015)', () => {
    const copy = stageCopy({ ...base, scene: 'Score' })
    expect(copy.detail).toContain('Players decide')
  })
})
