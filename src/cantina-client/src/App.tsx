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
  removeFromSetlist,
  searchSongs,
  recentAcquisitions,
  stripColorTags,
  type AcquisitionRecord,
  type CueStatus,
  advanceStatus,
  scoreContinue,
  setAdvance,
  type AdvanceStatus,
  type DebugView,
  type EncoreChart,
  type ProviderDownload,
  type SongInstruments,
  type StandInStatus,
  debugView,
  deviceLabel,
  fetchFavorites,
  setFavorite,
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

type Tab = 'stage' | 'find' | 'debug'

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
  const [tab, setTab] = useState<Tab>('stage')
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
  const [advance, setAdvanceState] = useState<AdvanceStatus | null>(null)
  const [ownLabel, setOwnLabel] = useState<string | null>(null)
  const [favorites, setFavorites] = useState<Set<string>>(new Set())
  const [favoritesOnly, setFavoritesOnly] = useState(false)
  const [groupByArtist, setGroupByArtist] = useState(false)
  const [standIn, setStandIn] = useState<StandInStatus | null>(null)
  const [standInBusy, setStandInBusy] = useState(false)
  const [continueBusy, setContinueBusy] = useState(false)
  const [dismissedArrivals, setDismissedArrivals] = useState<Set<string>>(() => {
    try {
      return new Set(JSON.parse(window.localStorage.getItem('cantina.dismissed-arrivals') ?? '[]') as string[])
    } catch {
      return new Set()
    }
  })

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

  // The debug and provider surfaces are config-gated server-side and 404 when off, so
  // one read at pairing decides which tabs exist at all.
  useEffect(() => {
    if (!paired) return
    debugView()
      .then(setDebug)
      .catch(() => setDebug(null))
    providerEnabled()
      .then(setProvider)
      .catch(() => setProvider(false))
    advanceStatus()
      .then(setAdvanceState)
      .catch(() => setAdvanceState(null))
    deviceLabel()
      .then(setOwnLabel)
      .catch(() => setOwnLabel(null))
    fetchFavorites()
      .then((list) => setFavorites(new Set(list)))
      .catch(() => {
        // The connection banner already reports an unreachable Barkeep.
      })
  }, [paired])

  // While armed, the advance loop's sentence changes with each episode.
  useEffect(() => {
    if (!paired || !advance?.enabled) return

    const timer = window.setInterval(() => {
      advanceStatus()
        .then(setAdvanceState)
        .catch(() => {
          // The connection banner already reports an unreachable Barkeep.
        })
    }, 5000)

    return () => window.clearInterval(timer)
  }, [paired, advance?.enabled])

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

  // New arrivals matter within a minute of downloading something; beyond that the feed
  // is history, so a slow poll is enough.
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

  const onToggleAdvance = () => {
    if (!advance) return
    setAdvance(!advance.enabled)
      .then(setAdvanceState)
      .catch((error: unknown) => {
        if (!unpair(error)) setActionError('The auto-advance toggle could not reach Barkeep.')
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

  const onContinue = () => {
    setContinueBusy(true)
    scoreContinue()
      .then((status) => {
        if (status.state !== 'sent' && !actionError) setActionError(status.detail)
      })
      .catch((error: unknown) => {
        if (!unpair(error)) setActionError('The continue could not reach Barkeep.')
      })
      .finally(() => setContinueBusy(false))
  }

  const onDismissArrival = (key: string) => {
    setDismissedArrivals((current) => {
      const next = new Set(current)
      next.add(key)
      try {
        window.localStorage.setItem('cantina.dismissed-arrivals', JSON.stringify([...next]))
      } catch {
        // Private browsing; dismissals just won't be remembered.
      }
      return next
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
        setJustAdded(song.location)
        window.setTimeout(() => {
          setJustAdded((current) => (current === song.location ? null : current))
        }, 2000)
      })
      .catch((error: unknown) => {
        if (!unpair(error)) setActionError('The setlist change could not reach Barkeep.')
      })
  }

  const onStar = (song: IndexedSong) => {
    const favored = !favorites.has(song.location)
    setFavorite(song.location, favored)
      .then((list) => setFavorites(new Set(list)))
      .catch((error: unknown) => {
        if (!unpair(error)) setActionError('The favorite could not reach Barkeep.')
      })
  }

  const onRemove = (index: number, location: string | null) => {
    setActionError(null)
    removeFromSetlist(index, location)
      .then(refreshSetlist)
      .catch((error: unknown) => {
        if (!unpair(error)) setActionError('The setlist change could not reach Barkeep.')
      })
  }

  // One arrival per file, newest wins: a retried import used to stack three identical
  // notifications with no way to clear them (operator feedback, 2026-08-30). The ✕
  // remembers per device.
  const seenFiles = new Set<string>()
  const visibleArrivals = arrivals.filter((arrival) => {
    if (dismissedArrivals.has(arrival.idempotencyKey) || seenFiles.has(arrival.fileName)) return false
    seenFiles.add(arrival.fileName)
    return true
  })

  // The library, as the toggles ask for it: optionally favorites-only, optionally
  // grouped under artist headers. Grouping preserves the search ranking within groups.
  const visibleSongs = favoritesOnly ? songs.filter((song) => favorites.has(song.location)) : songs
  const byArtist = new Map<string, IndexedSong[]>()
  for (const song of visibleSongs) {
    const group = byArtist.get(song.artist)
    if (group) group.push(song)
    else byArtist.set(song.artist, [song])
  }
  const groupedSongs: Array<[string | null, IndexedSong[]]> = groupByArtist
    ? [...byArtist.entries()].sort((a, b) => a[0].localeCompare(b[0]))
    : [[null, visibleSongs]]

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

  const banner = live ? stageCopy(live) : null
  const copy = connectionCopy(connection)
  const expiry = certificateNotice(certificate)

  return (
    <main>
      <header className="masthead">
        <span className="eyebrow">Cantina</span>
        {ownLabel && <span className="masthead__device">{ownLabel}</span>}
        <span
          className={`masthead__dot masthead__dot--${connection}`}
          title={copy.title}
          aria-label={copy.title}
        />
      </header>

      {(provider || debug?.enabled) && (
        <nav className="tabs" aria-label="Sections">
          <button
            type="button"
            className={tab === 'stage' ? 'tabs__active' : undefined}
            onClick={() => setTab('stage')}
          >
            Stage
          </button>
          {provider && (
            <button
              type="button"
              className={tab === 'find' ? 'tabs__active' : undefined}
              onClick={() => setTab('find')}
            >
              Find songs
            </button>
          )}
          {debug?.enabled && (
            <button
              type="button"
              className={tab === 'debug' ? 'tabs__active' : undefined}
              onClick={() => setTab('debug')}
            >
              Debug
            </button>
          )}
        </nav>
      )}

      {/* The live picture stays visible on every tab: it is the one thing the operator
          must never lose track of while a song could be running. */}
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

      {live?.scene === 'Score' && (
        <button type="button" className="primary stage__continue" onClick={onContinue} disabled={continueBusy}>
          {continueBusy ? 'Continuing…' : 'Continue past the score screen'}
        </button>
      )}

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

      {tab === 'stage' && (
        <>
          {visibleArrivals.length > 0 && (
            <section className="arrivals">
              {visibleArrivals.slice(0, 3).map((arrival) => (
                <p key={arrival.idempotencyKey} className={`arrivals__item arrivals__item--${arrival.outcome}`}>
                  <span>{arrivalCopy(arrival)}</span>
                  <button
                    type="button"
                    className="arrivals__dismiss"
                    aria-label="Dismiss"
                    onClick={() => onDismissArrival(arrival.idempotencyKey)}
                  >
                    ✕
                  </button>
                </p>
              ))}
            </section>
          )}

          {setlist && setlist.state.entries.length > 0 && (
            <section className="setlist">
              <h2>
                Setlist
                <span className="setlist__count">{setlist.state.entries.length}</span>
                {advance && advance.phase !== 'Unavailable' && (
                  <button
                    type="button"
                    className={`setlist__advance${advance.enabled ? ' setlist__advance--armed' : ''}`}
                    onClick={onToggleAdvance}
                  >
                    {advance.enabled ? 'Auto-advance: on' : 'Auto-advance: off'}
                  </button>
                )}
              </h2>
              {advance?.enabled && <p className="setlist__advance-detail">{advance.detail}</p>}
              <ol>
                {setlist.state.entries.map((entry, index) => (
                  <li
                    key={`${entry.location ?? entry.hash}-${index}`}
                    className={index === setlist.state.cursor ? 'setlist__current' : undefined}
                  >
                    <span>
                      {entry.title} — {entry.artist}
                    </span>
                    <button
                      type="button"
                      className="setlist__remove"
                      aria-label={`Remove ${entry.title}`}
                      onClick={() => onRemove(index, entry.location ?? null)}
                    >
                      ✕
                    </button>
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

            <div className="library__view">
              <button
                type="button"
                className={favoritesOnly ? 'library__toggle library__toggle--on' : 'library__toggle'}
                onClick={() => setFavoritesOnly((current) => !current)}
              >
                ★ Favorites
              </button>
              <button
                type="button"
                className={groupByArtist ? 'library__toggle library__toggle--on' : 'library__toggle'}
                onClick={() => setGroupByArtist((current) => !current)}
              >
                Group by artist
              </button>
            </div>

            {groupedSongs.map(([artist, grouped]) => (
              <div key={artist ?? 'flat'}>
                {artist !== null && <h3 className="songs__artist">{artist}</h3>}
                <ul className="songs">
                  {grouped.map((song) => (
                    <li key={song.location}>
                      <div className="songs__meta">
                        <strong>{song.title}</strong>
                        <span>
                          {song.artist}
                          {song.charter && ` · ${stripColorTags(song.charter)}`}
                        </span>
                        <InstrumentChips instruments={song.instruments} />
                      </div>
                      <div className="songs__actions">
                        <button
                          type="button"
                          className={favorites.has(song.location) ? 'songs__star songs__star--on' : 'songs__star'}
                          aria-label={favorites.has(song.location) ? `Unstar ${song.title}` : `Star ${song.title}`}
                          onClick={() => onStar(song)}
                        >
                          ★
                        </button>
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
              </div>
            ))}
          </section>
        </>
      )}

      {tab === 'find' && provider && (
        <section className="finder">
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

          {visibleArrivals.length > 0 && (
            <section className="arrivals">
              {visibleArrivals.slice(0, 3).map((arrival) => (
                <p key={arrival.idempotencyKey} className={`arrivals__item arrivals__item--${arrival.outcome}`}>
                  <span>{arrivalCopy(arrival)}</span>
                  <button
                    type="button"
                    className="arrivals__dismiss"
                    aria-label="Dismiss"
                    onClick={() => onDismissArrival(arrival.idempotencyKey)}
                  >
                    ✕
                  </button>
                </p>
              ))}
            </section>
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
                    {chart.inLibrary && <span className="chip chip--present">In your library</span>}
                    <InstrumentChips instruments={chart.instruments} />
                  </div>
                  <button type="button" onClick={() => onDownload(chart)}>
                    {chart.inLibrary ? 'Download anyway' : 'Download & queue'}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}

      {tab === 'debug' && debug?.enabled && (
        <section className="debug">
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
        </section>
      )}
    </main>
  )
}

/// Which instruments a chart supports, compact enough to sit on one row. A vocals chart
/// is what makes lyrics available — the criterion for picking between versions — so it
/// gets a named pill rather than a letter.
function InstrumentChips({ instruments }: { instruments: SongInstruments | undefined }) {
  if (!instruments) return null

  const charted: Array<{ label: string; diff: number }> = []
  if (instruments.guitar >= 0) charted.push({ label: 'G', diff: instruments.guitar })
  if (instruments.bass >= 0) charted.push({ label: 'B', diff: instruments.bass })
  if (instruments.drums >= 0) charted.push({ label: 'D', diff: instruments.drums })
  if (instruments.keys >= 0) charted.push({ label: 'K', diff: instruments.keys })

  if (charted.length === 0 && instruments.vocals < 0) return null

  return (
    <span className="chips">
      {charted.map((chip) => (
        <span key={chip.label} className="chip" title={chipTitle(chip.label, chip.diff)}>
          {chip.label}
          {chip.diff}
        </span>
      ))}
      {instruments.vocals >= 0 && (
        <span className="chip chip--lyrics" title={`Vocals ${instruments.vocals} — lyrics available`}>
          Lyrics
        </span>
      )}
    </span>
  )
}

function chipTitle(label: string, diff: number): string {
  const names: Record<string, string> = { G: 'Guitar', B: 'Bass', D: 'Drums', K: 'Keys' }
  return `${names[label]} difficulty ${diff}`
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

/// The pipeline's failure codes, as sentences. "refresh-unsafe" read as a scary mystery
/// on the iPad when it simply meant "not during a song" (operator feedback, 2026-08-30).
function failureCopy(code: string | null): string {
  switch (code) {
    case 'refresh-unsafe':
      return 'the library cannot re-sync while a song is playing. It imports on its own once the stage is idle.'
    case 'refresh-failed':
      return 'the library re-sync did not complete. It retries on its own.'
    default:
      return `could not be imported (${code ?? 'unknown'}). It retries on its own.`
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
      return `${name} arrived but ${failureCopy(arrival.failureCode)}`
    case 'Ambiguous':
      return `${name} arrived; the import needs a look (${arrival.failureCode ?? 'unknown'}).`
    default:
      return `${name}: ${arrival.outcome}.`
  }
}

export default App
