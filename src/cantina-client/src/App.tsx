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
  recentAcquisitions,
  stripColorTags,
  type AcquisitionRecord,
  type CueStatus,
  type DebugView,
  type EncoreChart,
  type ProviderDownload,
  type StandInStatus,
  debugView,
  providerDownload,
  providerDownloads,
  providerEnabled,
  providerSearch,
  standInForPlayers,
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
  const [justAdded, setJustAdded] = useState<string | null>(null)
  const [arrivals, setArrivals] = useState<AcquisitionRecord[]>([])
  const [debug, setDebug] = useState<DebugView | null>(null)
  const [provider, setProvider] = useState(false)
  const [findQuery, setFindQuery] = useState('')
  const [findBusy, setFindBusy] = useState(false)
  const [findResults, setFindResults] = useState<EncoreChart[] | null>(null)
  const [findError, setFindError] = useState<string | null>(null)
  const [downloads, setDownloads] = useState<ProviderDownload[]>([])
  const [standIn, setStandIn] = useState<StandInStatus | null>(null)
  const [standInBusy, setStandInBusy] = useState(false)

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

  // The debug surface is config-gated server-side and 404s when off, so one read at
  // pairing decides whether the section exists at all.
  useEffect(() => {
    if (!paired) return
    debugView()
      .then(setDebug)
      .catch(() => setDebug(null))
    providerEnabled()
      .then(setProvider)
      .catch(() => setProvider(false))
  }, [paired])

  // While a download is running the picture changes by the second; otherwise it is
  // history and the response that started it is enough.
  const downloading = downloads.some((download) => download.state === 'downloading')
  useEffect(() => {
    if (!paired || !downloading) return

    const timer = window.setInterval(() => {
      providerDownloads()
        .then(setDownloads)
        .catch(() => {
          // The connection banner already reports an unreachable Barkeep.
        })
    }, 3000)

    return () => window.clearInterval(timer)
  }, [paired, downloading])

  // New arrivals matter within a minute of downloading something in Geomitron Bridge;
  // beyond that the feed is history, so a slow poll is enough.
  useEffect(() => {
    if (!paired) return

    const read = () =>
      recentAcquisitions()
        .then(setArrivals)
        .catch(() => {
          // The connection banner already reports an unreachable Barkeep.
        })

    read()
    const timer = window.setInterval(read, 30 * 1000)
    return () => window.clearInterval(timer)
  }, [paired])

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

  const onFind = () => {
    const wanted = findQuery.trim()
    if (wanted.length === 0 || findBusy) return
    setFindBusy(true)
    setFindError(null)
    providerSearch(wanted)
      .then((result) => {
        if (result.refusal) {
          setFindResults(null)
          setFindError(result.refusal)
        } else {
          setFindResults(result.charts)
        }
      })
      .catch((error: unknown) => {
        if (!unpair(error)) setFindError('The search could not reach Barkeep.')
      })
      .finally(() => setFindBusy(false))
  }

  const onDownload = (chart: EncoreChart) => {
    providerDownload(chart)
      .then((download) =>
        setDownloads((current) => [download, ...current.filter((d) => d.md5 !== download.md5)]),
      )
      .catch((error: unknown) => {
        if (!unpair(error)) setFindError('The download request could not reach Barkeep.')
      })
  }

  const onStandIn = () => {
    setStandInBusy(true)
    setStandIn(null)
    standInForPlayers()
      .then(setStandIn)
      .catch((error: unknown) => {
        if (!unpair(error)) setStandIn({ state: 'failed', detail: 'The request could not reach Barkeep.' })
      })
      .finally(() => setStandInBusy(false))
  }

  const onAdd = (song: IndexedSong) => {
    setActionError(null)
    addToSetlist(song)
      .then(() => {
        refreshSetlist()
        // The tap that seems to do nothing was the complaint that drove this screen's
        // redesign: the add worked every time, and nothing said so.
        setJustAdded(song.location)
        window.setTimeout(() => {
          setJustAdded((current) => (current === song.location ? null : current))
        }, 2000)
      })
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
      <header className="masthead">
        <span className="eyebrow">Cantina</span>
        <span
          className={`masthead__dot masthead__dot--${connectionState}`}
          title={copy.title}
          aria-label={copy.title}
        />
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

      {arrivals.length > 0 && (
        <section className="arrivals">
          {arrivals.slice(0, 3).map((arrival) => (
            <p key={arrival.idempotencyKey} className={`arrivals__item arrivals__item--${arrival.outcome}`}>
              {arrivalCopy(arrival)}
            </p>
          ))}
        </section>
      )}

      {setlist && setlist.state.entries.length > 0 && (
        <section className="setlist">
          <h2>
            Setlist
            <span className="setlist__count">{setlist.state.entries.length}</span>
          </h2>
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
                  {justAdded === song.location ? 'Added ✓' : 'Add to setlist'}
                </button>
                <button type="button" className="primary" onClick={() => onCue(song)}>
                  Play now
                </button>
              </div>
            </li>
          ))}
        </ul>
      </section>

      {provider && (
        <section className="finder">
          <h2>Find new songs</h2>
          <p className="finder__hint">
            Searches Chorus Encore. A downloaded chart imports on its own and queues to
            play next.
          </p>
          <form
            className="finder__form"
            onSubmit={(event) => {
              event.preventDefault()
              onFind()
            }}
          >
            <input
              type="search"
              placeholder="Song or artist"
              value={findQuery}
              onChange={(event) => setFindQuery(event.target.value)}
              aria-label="Search Chorus Encore"
            />
            <button type="submit" disabled={findBusy}>
              {findBusy ? 'Searching…' : 'Search'}
            </button>
          </form>

          {findError && <p className="error">{findError}</p>}

          {downloads.length > 0 && (
            <ul className="finder__downloads">
              {downloads.slice(0, 4).map((download) => (
                <li key={download.md5} className={`finder__download finder__download--${download.state}`}>
                  {download.title} — {downloadCopy(download)}
                </li>
              ))}
            </ul>
          )}

          {findResults && findResults.length === 0 && <p>Nothing matched on Chorus Encore.</p>}

          {findResults && findResults.length > 0 && (
            <ul className="finder__results">
              {findResults.map((chart) => (
                <li key={chart.md5}>
                  <div>
                    <strong>{chart.name}</strong> — {chart.artist}
                    <span className="finder__meta">
                      {chart.charter ? ` ${chart.charter} · ` : ' '}
                      {lengthCopy(chart.songLengthMilliseconds)}
                    </span>
                  </div>
                  <button type="button" onClick={() => onDownload(chart)}>
                    Download &amp; queue
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}

      {debug?.enabled && (
        <details className="debug">
          <summary>Debugging</summary>
          <p>
            Bench testing only: stand in for the players&apos; ready confirms at instrument
            setup ({debug.playerConfirmations} players). Cue a song first; this kicks it off.
          </p>
          <button
            type="button"
            onClick={onStandIn}
            disabled={standInBusy || cue?.state !== 'pending-players'}
          >
            {standInBusy ? 'Confirming…' : 'Start the cued song'}
          </button>
          {standIn && (
            <p className={`debug__result debug__result--${standIn.state}`}>{standIn.detail}</p>
          )}
        </details>
      )}
    </main>
  )
}

function lengthCopy(milliseconds: number): string {
  const total = Math.round(milliseconds / 1000)
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`
}

function downloadCopy(download: ProviderDownload): string {
  switch (download.state) {
    case 'downloading':
      return 'downloading…'
    case 'delivered':
      return 'downloaded; importing now'
    default:
      return download.detail
  }
}

/// Plain words for an import outcome. The failure codes are the pipeline's own vocabulary;
/// the iPad gets a sentence.
function arrivalCopy(arrival: AcquisitionRecord): string {
  const name = arrival.fileName.replace(/\.sng$/i, '')

  switch (arrival.outcome) {
    case 'Completed':
      return `${name} arrived and is queued to play next.`
    case 'Failed':
      return `${name} arrived but could not be imported (${arrival.failureCode ?? 'unknown'}). It retries on its own.`
    case 'Ambiguous':
      return `${name} arrived; the import needs a look (${arrival.failureCode ?? 'unknown'}).`
    default:
      return `${name}: ${arrival.outcome}.`
  }
}

export default App
