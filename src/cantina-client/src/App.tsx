import './App.css'
import { connectionCopy, type ConnectionState } from './connectionState'

function App() {
  const state: ConnectionState = 'not-configured'
  const connection = connectionCopy(state)

  return (
    <main>
      <header>
        <p className="eyebrow">Cantina</p>
        <h1>Your setlist, within reach.</h1>
        <p className="lede">
          Browse the theater library and cue stock YARG from the iPad.
        </p>
      </header>

      <section className="connection" aria-live="polite">
        <span className={`connection__dot connection__dot--${state}`} />
        <div>
          <h2>{connection.title}</h2>
          <p>{connection.detail}</p>
        </div>
      </section>

      <p className="foundation-note">
        M0 foundation build. Library and control arrive only after the YARG spikes.
      </p>
    </main>
  )
}

export default App
