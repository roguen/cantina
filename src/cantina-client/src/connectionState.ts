// SPDX-License-Identifier: LGPL-3.0-or-later

export type ConnectionState =
  | 'not-configured'
  | 'connecting'
  | 'connected'
  | 'disconnected'

type ConnectionCopy = {
  title: string
  detail: string
}

const copy: Record<ConnectionState, ConnectionCopy> = {
  'not-configured': {
    title: 'Barkeep is not configured',
    detail: 'The song library is unavailable until this iPad is paired.',
  },
  connecting: {
    title: 'Connecting to Barkeep',
    detail: 'Your setlist will appear when the theater PC responds.',
  },
  connected: {
    title: 'Connected to Barkeep',
    detail: 'The theater PC is available.',
  },
  disconnected: {
    title: 'Barkeep is offline',
    detail: 'Your library is not empty—the theater PC cannot be reached.',
  },
}

export function connectionCopy(state: ConnectionState): ConnectionCopy {
  return copy[state]
}
