// SPDX-License-Identifier: LGPL-3.0-or-later

import { useCallback, useEffect, useState } from 'react'
import './App.css'
import {
  NotPairedError,
  addToSetlist,
  cueSong,
  currentCue,
  fetchHealth,
  fetchSetlist,
  searchSongs,
  stripColorTags,
  type CueStatus,
  type IndexedSong,
  type SetlistView,
} from './api'
import { certificateNotice, type CertificateHealth } from './certificateNotice'
import { PairingScreen } from './PairingScreen'
import { connectionCopy } from './connectionState'
import { stageCopy } from './liveState'
import { storedToken } from './pairing'
import { useLiveSocket } from './useLiveSocket'

function App() {
  // Pairing is a state of the app, not an error in it. Barkeep answering 401 anywhere
  // means this device's credential is gone — revoked at the theater PC, most likely — and
  // the only useful screen is the one asking for a new code (D-026).
  const [paired, setPaired] = useState(() => storedToken() !== null)
  const [pairingDetail, setPairingDetail] = useState<string | null>(null)

  const unpair = useCallback((error: unknown) => {
    if (error instanceof NotPairedError) {
      setPaired(false)
      setPairingDetail(error.message)
      return true
    }
    return false
  }, [])

  const { connection, live } = useLiveSocket(
    paired,
    useCallback(() => setPaired(false), []),
  )
  const [query, setQuery] = useState('')
  const [songs, setSongs] = useState<IndexedSong[]>([])
  const [totalIndexed, setTotalIndexed] = useState<number | null>(null)
  const [searchError, setSearchError] = useState<string | null>(null)
  const [setlist, setSetlist] = useState<SetlistView | null>(null)
  const [cue, setCue] = useState<CueStatus | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [certificate, setCertificate] = useState<CertificateHealth | null>(null)

  const refreshSetlist = useCallback(() => {
    fetchSetlist()
      .then(setSetlist)
      .catch((error: unknown) => {
        if (!unpair(error)) setSetlist(null)
      })
  }, [unpair])

  useEffect(() => {
    if (paired) refreshSetlist()
  }, [paired, refreshSetlist])

  // Certificate life changes on the scale of days, so once an hour is plenty and anything
  // more often is noise on a device that spends most of its time asleep.
  useEffect(() => {
    if (!paired) return

    const read = () =>
      fetchHealth()
        .then((health) => setCertificate(health.certificate))
        .catch(() => {
          // The connection banner already reports an unreachable Barkeep.
        })

    read()
    const timer = window.setInterval(read, 60 * 60 * 1000)
    return () => window.clearInterval(timer)
  }, [paired])

  // Debounced search against the index.
  useEffect(() => {
    if (!paired) return

    const timer = window.setTimeout(() => {
      searchSongs(query)
        .then((response) => {
          setSongs(response.results)
          setTotalIndexed(response.totalIndexed)
          setSearchError(null)
        })
        .catch((error: unknown) => {
          if (!unpair(error)) setSearchError('The library is unreachable.')
        })
    }, 200)

    return () => window.clearTimeout(timer)
  }, [query, connection, paired, unpair])

  // Follow an in-flight cue until it resolves; resolution arrives by observation
  // (pending-players is a real state, not a spinner).
  useEffect(() => {
    if (cue?.state !== 'pending-players') return

    const timer = window.setInterval(() => {
      currentCue()
        .then((status) => {
          if (status) setCue(status)
        })
        .catch(() => {
          // Barkeep unreachable; the connection banner already says so.
        })
    }, 1000)

    return () => window.clearInterval(timer)
  }, [cue?.state, cue?.commandId])

  const onCue = (song: IndexedSong) => {
    setActionError(null)
    cueSong(song)
      .then(setCue)
      .catch((error: unknown) => {
        if (!unpair(error)) setActionError('The cue could not reach Barkeep.')
      })
  }

  const onAdd = (song: IndexedSong) => {
    setActionError(null)
    addToSetlist(song)
      .then(refreshSetlist)
      .catch((error: unknown) => {
        if (!unpair(error)) setActionError('The setlist change could not reach Barkeep.')
      })
  }

  if (!paired) {
    return (
      <PairingScreen
        detail={pairingDetail}
        onPaired={() => {
          setPairingDetail(null)
          setPaired(true)
        }}
      />
    )
  }

  const connectionState = connection
  const banner = live ? stageCopy(live) : null
  const copy = connectionCopy(connectionState)
  const expiry = certificateNotice(certificate)

  return (
    <main>
      <header>
        <p className="eyebrow">Cantina</p>
        <h1>Your setlist, within reach.</h1>
      </header>

      <section className={`stage stage--${banner?.tone ?? 'down'}`} aria-live="polite">
        {banner ? (
          <div>
            <h2>{banner.headline}</h2>
            <p>{banner.detail}</p>
          </div>
        ) : (
          <div>
            <h2>{copy.title}</h2>
            <p>{copy.detail}</p>
          </div>
        )}
      </section>

      {expiry && (
        <section className="cue cue--failed" aria-live="polite">
          <h2>{expiry.headline}</h2>
          <p>{expiry.detail}</p>
        </section>
      )}

      {cue && (
        <section className={`cue cue--${cue.state}`} aria-live="polite">
          <h2>
            {cue.state === 'pending-players' && `Cued: ${cue.requested.title} — waiting on the players`}
            {cue.state === 'done' && `Playing: ${cue.requested.title}`}
            {cue.state === 'failed' && `Cue failed: ${cue.requested.title}`}
            {cue.state === 'refused' && 'Cue refused'}
            {cue.state === 'replayed' && 'Already handled'}
          </h2>
          <p>{cue.detail}</p>
        </section>
      )}

      {actionError && <p className="error">{actionError}</p>}

      <section className="library">
        <input
          type="search"
          placeholder={totalIndexed === null ? 'Search the library' : `Search ${totalIndexed} songs`}
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          aria-label="Search the library"
        />

        {searchError && <p className="error">{searchError}</p>}

        <ul className="songs">
          {songs.map((song) => (
            <li key={song.location}>
              <div className="songs__meta">
                <strong>{song.title}</strong>
                <span>
                  {song.artist}
                  {song.charter && ` · ${stripColorTags(song.charter)}`}
                </span>
              </div>
              <div className="songs__actions">
                <button type="button" onClick={() => onAdd(song)}>
                  Add
                </button>
                <button type="button" className="primary" onClick={() => onCue(song)}>
                  Cue
                </button>
              </div>
            </li>
          ))}
        </ul>
      </section>

      {setlist && setlist.state.entries.length > 0 && (
        <section className="setlist">
          <h2>Setlist</h2>
          <ol>
            {setlist.state.entries.map((entry, index) => (
              <li
                key={`${entry.location ?? entry.hash}-${index}`}
                className={index === setlist.state.cursor ? 'setlist__current' : undefined}
              >
                {entry.title} — {entry.artist}
              </li>
            ))}
          </ol>
          {setlist.quarantinedFiles.length > 0 && (
            <p className="error">Setlist state was recovered; a damaged file was set aside.</p>
          )}
        </section>
      )}
    </main>
  )
}

export default App
