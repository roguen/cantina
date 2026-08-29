// SPDX-License-Identifier: LGPL-3.0-or-later

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { forgetToken, pair, pairingRefusal, rememberToken, storedToken } from './pairing'

// These tests run without a DOM, so the storage the client actually uses is supplied here
// rather than assumed. That is the point: a browser that refuses site data looks exactly
// like the absent case, and the client has to keep working either way.
function memoryStorage(): Storage {
  const entries = new Map<string, string>()
  return {
    get length() {
      return entries.size
    },
    clear: () => entries.clear(),
    getItem: (key: string) => entries.get(key) ?? null,
    key: (index: number) => [...entries.keys()][index] ?? null,
    removeItem: (key: string) => void entries.delete(key),
    setItem: (key: string, value: string) => void entries.set(key, value),
  }
}

beforeEach(() => {
  vi.stubGlobal('localStorage', memoryStorage())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the device credential', () => {

  it('survives being written and read back', () => {
    rememberToken('a-token')
    expect(storedToken()).toBe('a-token')

    forgetToken()
    expect(storedToken()).toBeNull()
  })

  it('reports no token rather than throwing when site data is unavailable', () => {
    // Private browsing throws from localStorage rather than returning null. An iPad that
    // cannot remember a token must still be able to pair, not show a crash.
    vi.stubGlobal('localStorage', {
      getItem: () => {
        throw new Error('access denied')
      },
      setItem: () => {
        throw new Error('access denied')
      },
      removeItem: () => {
        throw new Error('access denied')
      },
    })

    expect(storedToken()).toBeNull()
    expect(() => rememberToken('a-token')).not.toThrow()
  })
})

describe('pairing refusals', () => {
  it('names each refusal in the operator’s terms and says what to do next', async () => {
    const cases = [
      ['NoWindowOpen', 'Open one on the theater PC.'],
      ['Expired', 'Open a new pairing window'],
      ['WrongCode', 'not right'],
      ['TooManyAttempts', 'window closed'],
    ] as const

    for (const [reason, expected] of cases) {
      vi.stubGlobal(
        'fetch',
        vi.fn(async () => new Response(JSON.stringify({ reason }), { status: 403 })),
      )

      const outcome = await pair('AAAAAAAA', 'iPad')

      expect(outcome.ok).toBe(false)
      if (!outcome.ok) expect(outcome.detail).toContain(expected)
    }
  })

  it('treats a rate-limited attempt as a closed window, because that is what it means', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('', { status: 429 })))

    const outcome = await pair('AAAAAAAA', 'iPad')

    expect(outcome.ok).toBe(false)
    if (!outcome.ok) expect(outcome.detail).toContain('theater PC')
  })

  it('keeps the token from a successful pairing, and only then', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(JSON.stringify({ token: 'granted' }), { status: 200 })),
    )

    expect((await pair('ABCD2345', 'iPad')).ok).toBe(true)
    expect(storedToken()).toBe('granted')
  })
})

describe('an unauthorised answer', () => {
  it('distinguishes a stale socket ticket from a revoked device', () => {
    expect(pairingRefusal(401, '{"reason":"ticket-required"}')).toContain('ticket')
    expect(pairingRefusal(401, '{"reason":"pairing-required"}')).toContain('not paired')
    expect(pairingRefusal(200, '')).toBeNull()
  })
})
