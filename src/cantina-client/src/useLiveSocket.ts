// SPDX-License-Identifier: LGPL-3.0-or-later

import { useEffect, useRef, useState } from 'react'
import { NotPairedError, liveTicket } from './api'
import type { ConnectionState } from './connectionState'
import type { LiveState } from './liveState'

type LiveFeed = {
  connection: ConnectionState
  live: LiveState | null
}

// Subscribes to /ws/live with automatic reconnection. Connection state is reported
// honestly and separately from YARG state: Barkeep being reachable and YARG being
// observable are different facts, and conflating them is how a dead stage gets rendered
// as an empty library.
//
// Each connection is bought with a fresh single-use ticket (D-026), because a browser
// cannot put a credential on a WebSocket. An iPad waking from sleep therefore takes one
// extra round trip and no ceremony, and a ticket that leaked into a log is already spent.
export function useLiveSocket(paired: boolean, onNotPaired: () => void): LiveFeed {
  const [connection, setConnection] = useState<ConnectionState>('connecting')
  const [live, setLive] = useState<LiveState | null>(null)
  const retryDelay = useRef(1000)
  const notPaired = useRef(onNotPaired)
  notPaired.current = onNotPaired

  useEffect(() => {
    if (!paired) {
      setConnection('not-configured')
      setLive(null)
      return
    }

    let socket: WebSocket | null = null
    let retryTimer: number | undefined
    let disposed = false

    const retry = () => {
      if (disposed) return
      retryTimer = window.setTimeout(connect, retryDelay.current)
      retryDelay.current = Math.min(retryDelay.current * 2, 15000)
    }

    const connect = () => {
      if (disposed) return
      setConnection('connecting')

      liveTicket()
        .then((ticket) => {
          if (disposed) return

          const scheme = window.location.protocol === 'https:' ? 'wss' : 'ws'
          socket = new WebSocket(
            `${scheme}://${window.location.host}/ws/live?ticket=${encodeURIComponent(ticket)}`,
          )

          socket.onopen = () => {
            retryDelay.current = 1000
            setConnection('connected')
          }

          socket.onmessage = (event) => {
            try {
              setLive(JSON.parse(event.data as string) as LiveState)
            } catch {
              // A malformed frame is dropped; the next heartbeat re-delivers state.
            }
          }

          socket.onclose = () => {
            if (disposed) return
            setConnection('disconnected')
            retry()
          }
        })
        .catch((error: unknown) => {
          if (disposed) return

          // Revoked at the theater PC is not a network problem, and retrying forever
          // would hide it behind a reconnect spinner.
          if (error instanceof NotPairedError) {
            setConnection('not-configured')
            notPaired.current()
            return
          }

          setConnection('disconnected')
          retry()
        })
    }

    connect()

    return () => {
      disposed = true
      window.clearTimeout(retryTimer)
      socket?.close()
    }
  }, [paired])

  return { connection, live }
}
