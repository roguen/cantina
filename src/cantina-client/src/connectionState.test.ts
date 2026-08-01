// SPDX-License-Identifier: LGPL-3.0-or-later

import { describe, expect, it } from 'vitest'
import { connectionCopy } from './connectionState'

describe('connection copy', () => {
  it('does not present a dead server as an empty library', () => {
    const copy = connectionCopy('disconnected')

    expect(copy.title).toBe('Barkeep is offline')
    expect(copy.detail).toContain('not empty')
  })
})
