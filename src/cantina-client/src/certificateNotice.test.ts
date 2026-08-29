// SPDX-License-Identifier: LGPL-3.0-or-later

import { describe, expect, it } from 'vitest'
import { certificateNotice, type CertificateHealth } from './certificateNotice'

const base: CertificateHealth = {
  source: 'supplied',
  needsDeviceTrust: false,
  notAfter: '2026-11-27T00:00:00Z',
  daysRemaining: 89,
  status: 'ok',
}

describe('the certificate notice', () => {
  it('says nothing while renewal is keeping up', () => {
    expect(certificateNotice(base)).toBeNull()
    expect(certificateNotice(null)).toBeNull()
  })

  it('names the renewal machinery when the certificate came from outside', () => {
    const notice = certificateNotice({ ...base, daysRemaining: 14, status: 'expiring' })

    expect(notice?.headline).toContain('14 days')
    expect(notice?.detail).toContain('renewal on the NAS')
  })

  it('says the opposite for the theater authority, which Barkeep renews itself', () => {
    const notice = certificateNotice({
      ...base,
      source: 'theater-authority',
      needsDeviceTrust: true,
      daysRemaining: 9,
      status: 'expiring',
    })

    expect(notice?.detail).toContain('reissues this one itself')
    expect(notice?.detail).not.toContain('NAS')
  })

  it('gets the singular right, because a warning with a typo reads as noise', () => {
    expect(certificateNotice({ ...base, daysRemaining: 1, status: 'expiring' })?.headline)
      .toContain('1 day')
    expect(certificateNotice({ ...base, daysRemaining: 1, status: 'expiring' })?.headline)
      .not.toContain('1 days')
  })

  it('drops the countdown once it has lapsed rather than counting down past zero', () => {
    const notice = certificateNotice({ ...base, daysRemaining: -3, status: 'expired' })

    expect(notice?.headline).toBe('The theater certificate has lapsed')
  })
})
