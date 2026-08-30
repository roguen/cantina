// SPDX-License-Identifier: LGPL-3.0-or-later

import { useEffect, useState } from 'react'
import { pair, pairingEmailEnabled, requestPairingEmail } from './pairing'

type Props = {
  detail: string | null
  onPaired: () => void
}

// The first thing this iPad ever sees, and the last thing it sees if the theater PC
// revokes it. It asks for one thing and explains where that thing comes from, because the
// code is deliberately not discoverable from here: it is shown on the theater PC, and
// standing in the room is what authorises a new device (D-026).
export function PairingScreen({ detail, onPaired }: Props) {
  const [code, setCode] = useState('')
  const [label, setLabel] = useState('iPad')
  const [refusal, setRefusal] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [emailAvailable, setEmailAvailable] = useState(false)
  const [emailAddress, setEmailAddress] = useState('')
  const [emailStatus, setEmailStatus] = useState<string | null>(null)
  const [emailBusy, setEmailBusy] = useState(false)

  useEffect(() => {
    pairingEmailEnabled().then(setEmailAvailable)
  }, [])

  const requestEmail = () => {
    setEmailBusy(true)
    setEmailStatus(null)
    requestPairingEmail(emailAddress.trim() || null)
      .then((outcome) =>
        setEmailStatus(
          outcome.state === 'sent' ? 'Sent — check the email account for the code.' : outcome.detail,
        ),
      )
      .catch(() => setEmailStatus('The theater PC could not be reached.'))
      .finally(() => setEmailBusy(false))
  }

  const submit = (event: React.FormEvent) => {
    event.preventDefault()
    setSubmitting(true)
    setRefusal(null)

    pair(code.trim(), label.trim() || 'iPad')
      .then((outcome) => {
        if (outcome.ok) {
          onPaired()
          return
        }
        setRefusal(outcome.detail)
        setCode('')
      })
      .catch(() => setRefusal('The theater PC could not be reached.'))
      .finally(() => setSubmitting(false))
  }

  return (
    <main>
      <header>
        <p className="eyebrow">Cantina</p>
        <h1>Pair this iPad.</h1>
      </header>

      <section className="pairing">
        <p>
          On the theater PC, open a pairing window. Barkeep prints an eight-character code
          there{emailAvailable ? ' — or have it emailed to the address configured on the theater PC.' : ' — and only there.'}
        </p>

        {emailAvailable && (
          <div className="pairing__email">
            <input
              type="email"
              value={emailAddress}
              onChange={(event) => setEmailAddress(event.target.value)}
              placeholder="you@example.com (optional)"
              aria-label="Email address for the code"
              autoComplete="email"
            />
            <button type="button" onClick={requestEmail} disabled={emailBusy}>
              {emailBusy ? 'Sending…' : 'Email me a code'}
            </button>
          </div>
        )}
        {emailStatus && <p>{emailStatus}</p>}

        <form onSubmit={submit}>
          <label htmlFor="pairing-code">Pairing code</label>
          <input
            id="pairing-code"
            className="pairing__code"
            value={code}
            onChange={(event) => setCode(event.target.value.toUpperCase())}
            autoComplete="off"
            autoCapitalize="characters"
            autoCorrect="off"
            spellCheck={false}
            inputMode="text"
            maxLength={12}
            placeholder="ABCD2345"
          />

          <label htmlFor="pairing-label">Name this device</label>
          <input
            id="pairing-label"
            value={label}
            onChange={(event) => setLabel(event.target.value)}
            maxLength={40}
          />

          <button type="submit" className="primary" disabled={submitting || code.trim().length === 0}>
            {submitting ? 'Pairing…' : 'Pair'}
          </button>
        </form>

        {refusal && <p className="error">{refusal}</p>}
        {!refusal && detail && <p className="error">{detail}</p>}
      </section>
    </main>
  )
}
