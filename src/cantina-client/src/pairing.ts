// SPDX-License-Identifier: LGPL-3.0-or-later

// The device credential, and the one decision the client makes about it: hold it, send it,
// and throw it away the moment Barkeep says it is no longer a credential (D-026).
//
// The token lives in localStorage because the alternative is asking the operator to pair
// every time the iPad sleeps, and because there is nowhere better on a device Cantina does
// not control. A stolen iPad is a paired iPad — revocation at the theater PC is the answer
// to that, not client-side storage cleverness.

const TokenKey = 'cantina.deviceToken'

// Reached through globalThis rather than window so the storage boundary is one seam that
// a test can replace, instead of a browser global the tests would need a DOM to have.
function store(): Storage | undefined {
  return (globalThis as { localStorage?: Storage }).localStorage
}

export type PairingState =
  | { kind: 'unknown' }
  | { kind: 'unpaired'; detail: string | null }
  | { kind: 'paired' }

export function storedToken(): string | null {
  try {
    return store()?.getItem(TokenKey) ?? null
  } catch {
    // Private browsing and disabled site data both throw rather than return null. An iPad
    // that cannot remember a token can still pair for this session.
    return null
  }
}

export function rememberToken(token: string): void {
  try {
    store()?.setItem(TokenKey, token)
  } catch {
    // Nothing to do: the token stays in memory for this page's lifetime.
  }
}

export function forgetToken(): void {
  try {
    store()?.removeItem(TokenKey)
  } catch {
    // Already gone as far as this device is concerned.
  }
}

// Barkeep's named refusals. `pairing-required` means the credential is not one any more —
// revoked at the theater PC, or from a registry that was reset — and the only honest
// response is to forget it and ask the operator for a code.
export function pairingRefusal(status: number, body: string): string | null {
  if (status !== 401) return null
  if (body.includes('ticket-required')) return 'This connection needs a fresh ticket.'
  return 'This iPad is not paired with the theater PC.'
}

export type PairingOutcome =
  | { ok: true }
  | { ok: false; detail: string }

// The reasons Barkeep gives, in the operator's words rather than the protocol's. Each one
// says what to do next, because "Forbidden" does not.
const refusals: Record<string, string> = {
  NoWindowOpen: 'No pairing window is open. Open one on the theater PC.',
  Expired: 'That code has expired. Open a new pairing window on the theater PC.',
  WrongCode: 'That code is not right. Check the theater PC and try again.',
  TooManyAttempts: 'Too many wrong codes. The window closed — open a new one on the theater PC.',
}

export async function pair(code: string, label: string): Promise<PairingOutcome> {
  const response = await fetch('/api/pair', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code, label }),
  })

  if (response.ok) {
    const grant = (await response.json()) as { token: string }
    rememberToken(grant.token)
    return { ok: true }
  }

  if (response.status === 429) {
    return { ok: false, detail: refusals.TooManyAttempts }
  }

  try {
    const refused = (await response.json()) as { reason?: string }
    return { ok: false, detail: refusals[refused.reason ?? ''] ?? 'The theater PC refused the pairing.' }
  } catch {
    return { ok: false, detail: 'The theater PC refused the pairing.' }
  }
}
