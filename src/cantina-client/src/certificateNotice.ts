// SPDX-License-Identifier: LGPL-3.0-or-later

// The client half of D-029's renewal tripwire.
//
// A publicly trusted certificate is renewed by machinery Barkeep does not own and cannot
// see. When that machinery stops, nothing looks wrong until the day the certificate lapses
// and every device refuses to connect at once — including this one, which is why the client
// has to say something while it still *can* connect.
//
// There is deliberately no "expired" copy. An expired certificate means the TLS handshake
// fails, so the client is not running to render anything. The only useful window is the one
// before that, and it is the only one this file has words for.

export type CertificateHealth = {
  source: 'supplied' | 'theater-authority'
  needsDeviceTrust: boolean
  notAfter: string
  daysRemaining: number
  status: 'ok' | 'expiring' | 'expired'
}

export type CertificateNotice = {
  headline: string
  detail: string
}

export function certificateNotice(certificate: CertificateHealth | null): CertificateNotice | null {
  if (!certificate || certificate.status === 'ok') return null

  const days = certificate.daysRemaining

  // Whose problem it is differs by source, and saying so is the difference between a
  // warning somebody acts on and one they scroll past.
  if (certificate.source === 'supplied') {
    return {
      headline:
        days <= 0
          ? 'The theater certificate has lapsed'
          : `The theater certificate expires in ${days} ${days === 1 ? 'day' : 'days'}`,
      detail:
        'Automatic renewal has not landed. Cantina stops accepting connections when it lapses — check the renewal on the NAS.',
    }
  }

  return {
    headline:
      days <= 0
        ? 'The theater certificate has lapsed'
        : `The theater certificate expires in ${days} ${days === 1 ? 'day' : 'days'}`,
    detail:
      'Barkeep reissues this one itself and normally needs no help. If this persists, restart Barkeep on the theater PC.',
  }
}
