// SPDX-License-Identifier: LGPL-3.0-or-later

import { useEffect, useRef, useState } from 'react'
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
export function useLiveSocket(): LiveFeed {
  const [connection, setConnection] = useState<ConnectionState>('connecting')
  const [live, setLive] = useState<LiveState | null>(null)
  const retryDelay = useRef(1000)

  useEffect(() => {
    let socket: WebSocket | null = null
    let retryTimer: number | undefined
    let disposed = false

    const connect = () => {
      if (disposed) return
      setConnection('connecting')

      const scheme = window.location.protocol === 'https:' ? 'wss' : 'ws'
      socket = new WebSocket(`${scheme}://${window.location.host}/ws/live`)

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
        retryTimer = window.setTimeout(connect, retryDelay.current)
        retryDelay.current = Math.min(retryDelay.current * 2, 15000)
      }
    }

    connect()

    return () => {
      disposed = true
      window.clearTimeout(retryTimer)
      socket?.close()
    }
  }, [])

  return { connection, live }
}
