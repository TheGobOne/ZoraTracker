'use client'

import { MAX_SPOTS } from '@/lib/data'
import { formatTime, useElapsed, useTracker, type TimelineEvent } from '@/lib/tracker-state'

const KIND_COLOR: Record<TimelineEvent['kind'], string> = {
  triforce: '#f2c94c',
  item: '#ef8300',
  dungeon: '#cfd6e3',
  sword: '#5b8def',
  road: '#e879b0',
}

const VIEW_W = 1000
const VIEW_H = 120
const PAD_X = 16
const AXIS_Y = 98

export function Timeline() {
  const { state } = useTracker()
  const elapsed = useElapsed()

  // The axis grows in 20 minute steps, with a 2 hour floor like the desktop tracker.
  const lastEvent = state.events.reduce((m, e) => Math.max(m, e.t), 0)
  const spanMin = Math.max(120, Math.ceil(Math.max(elapsed, lastEvent) / 60000 / 20) * 20)
  const x = (ms: number) => PAD_X + (ms / 60000 / spanMin) * (VIEW_W - PAD_X * 2)

  const total = MAX_SPOTS[state.quest]
  const history = [{ t: 0, n: 0 }, ...state.owHistory]
  const linePoints =
    history.length > 1
      ? [...history, { t: Math.max(elapsed, lastEvent), n: history[history.length - 1].n }]
          .map((p) => `${x(p.t)},${AXIS_Y - Math.min(1, p.n / total) * 70}`)
          .join(' ')
      : ''

  const ticks: number[] = []
  for (let m = 0; m <= spanMin; m += 20) ticks.push(m)

  return (
    <section className="panel p-3" aria-labelledby="timeline-title">
      <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
        <h2 id="timeline-title" className="section-title">
          Timeline
        </h2>
        <ul className="flex flex-wrap gap-x-3 gap-y-1 text-[11px] text-muted">
          {(Object.keys(KIND_COLOR) as TimelineEvent['kind'][]).map((k) => (
            <li key={k} className="flex items-center gap-1.5 capitalize">
              <span className="inline-block size-2" style={{ background: KIND_COLOR[k] }} />
              {k === 'road' ? 'any road' : k === 'sword' ? 'sword cave' : k}
            </li>
          ))}
          <li className="flex items-center gap-1.5">
            <span className="inline-block h-0.5 w-3 bg-good" />
            overworld progress
          </li>
        </ul>
      </div>

      <svg
        viewBox={`0 0 ${VIEW_W} ${VIEW_H}`}
        className="h-auto w-full border border-line bg-black"
        role="img"
        aria-label={`Run timeline with ${state.events.length} events`}
      >
        {ticks.map((m) => (
          <g key={m}>
            <line x1={x(m * 60000)} x2={x(m * 60000)} y1={20} y2={AXIS_Y} stroke="#1f2417" />
            <text x={x(m * 60000)} y={VIEW_H - 4} fill="#8a917a" fontSize="10" textAnchor="middle">
              {m === 0 ? '0' : `${Math.floor(m / 60)}:${String(m % 60).padStart(2, '0')}`}
            </text>
          </g>
        ))}
        <line x1={PAD_X} x2={VIEW_W - PAD_X} y1={AXIS_Y} y2={AXIS_Y} stroke="#2b3220" />

        {linePoints && (
          <polyline points={linePoints} fill="none" stroke="#4ade80" strokeWidth="1.5" opacity="0.7" />
        )}

        {state.events.map((e, i) => {
          const cx = x(e.t)
          // Stagger labels so close-together events stay readable.
          const cy = 18 + (i % 4) * 16
          return (
            <g key={i}>
              <title>{`${formatTime(e.t)}  ${e.label}`}</title>
              <line x1={cx} x2={cx} y1={cy} y2={AXIS_Y} stroke={KIND_COLOR[e.kind]} opacity="0.4" />
              <rect x={cx - 9} y={cy - 8} width="18" height="14" fill="#0a0c08" stroke={KIND_COLOR[e.kind]} />
              <text
                x={cx}
                y={cy + 3}
                fill={KIND_COLOR[e.kind]}
                fontSize="8"
                textAnchor="middle"
                fontFamily="monospace"
              >
                {e.label}
              </text>
            </g>
          )
        })}

        {elapsed > 0 && (
          <line x1={x(elapsed)} x2={x(elapsed)} y1={4} y2={AXIS_Y} stroke="#ef8300" strokeDasharray="3 3" />
        )}
      </svg>

      {state.events.length === 0 && (
        <p className="mt-2 text-[11px] text-muted">
          Start the timer, then triforce pieces, dungeon items, dungeons found and caves will be
          logged here as you mark them.
        </p>
      )}
    </section>
  )
}
