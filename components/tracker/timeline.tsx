'use client'

import { MAX_SPOTS } from '@/lib/data'
import { useElapsed, useTracker, type TimelineEvent } from '@/lib/tracker-state'
import { at } from './header'
import { ItemIcon, Sprite } from './sprite'

const TOP = 842
const HEIGHT = 125
const X0 = 16
const PX_PER_MIN = 6.1
const BASE_Y = TOP + 112
const CURVE_H = 70
const MARKER_LANES = [-6, -32, -58]

const xAt = (ms: number) => Math.min(X0 + (ms / 60000) * PX_PER_MIN, 754)

function EventMarker({ event }: { event: TimelineEvent }) {
  switch (event.kind) {
    case 'triforce':
      return <Sprite sheet="icons10" index={1} scale={2} />
    case 'item':
      return <ItemIcon code={event.label} />
    default: {
      const color = event.kind === 'dungeon' ? '#ffff00' : event.kind === 'road' ? '#da70d6' : '#e6e6e6'
      return (
        <div
          style={{
            width: 16,
            height: 22,
            background: color,
            color: '#000',
            fontWeight: 700,
            fontSize: event.label.length > 1 ? 9 : 14,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          {event.label}
        </div>
      )
    }
  }
}

export function Timeline() {
  const { state } = useTracker()
  const elapsed = useElapsed()
  const max = MAX_SPOTS[state.quest === 'unknown' ? 'first' : state.quest]

  const points = state.owHistory.map((p) => `${xAt(p.t)},${BASE_Y - Math.min(1, p.n / max) * CURVE_H}`)
  const last = state.owHistory[state.owHistory.length - 1]
  const path = points.length
    ? `${X0},${BASE_Y} ${points.join(' ')} ${xAt(Math.max(elapsed, last?.t ?? 0))},${BASE_Y - Math.min(1, (last?.n ?? 0) / max) * CURVE_H}`
    : ''

  const progressAt = (t: number) => {
    let n = 0
    for (const p of state.owHistory) if (p.t <= t) n = p.n
    return Math.min(1, n / max) * CURVE_H
  }

  return (
    <>
      <div style={{ ...at(0, TOP, 768, HEIGHT), background: '#000' }} />
      <svg
        role="img"
        aria-label="Run timeline"
        width={768}
        height={HEIGHT}
        style={{ ...at(0, TOP), pointerEvents: 'none' }}
      >
        <line x1={X0} x2={X0 + 120 * PX_PER_MIN} y1={BASE_Y - TOP + 4} y2={BASE_Y - TOP + 4} stroke="#ffa500" strokeWidth={2} />
        {Array.from({ length: 25 }, (_, i) => {
          const x = X0 + i * 5 * PX_PER_MIN
          const major = i % 4 === 0
          return (
            <g key={i}>
              <line x1={x} x2={x} y1={BASE_Y - TOP + 4} y2={BASE_Y - TOP + (major ? 12 : 8)} stroke="#ffa500" strokeWidth={2} />
              {major && i > 0 && (
                <text x={x} y={BASE_Y - TOP + 22} fill="#ffa500" fontSize={11} textAnchor="middle">
                  {`${Math.floor(i / 12)}:${String((i % 12) * 5).padStart(2, '0')}`}
                </text>
              )}
            </g>
          )
        })}
        {path && (
          <polyline
            points={path
              .split(' ')
              .map((p) => {
                const [x, y] = p.split(',')
                return `${x},${Number(y) - TOP}`
              })
              .join(' ')}
            fill="none"
            stroke="#2fb8b0"
            strokeWidth={2}
          />
        )}
        {state.timer.startedAt !== null && (
          <line x1={xAt(elapsed)} x2={xAt(elapsed)} y1={0} y2={HEIGHT - 20} stroke="#4aa3ff" strokeDasharray="3 3" />
        )}
      </svg>
      <div style={{ ...at(4, TOP + 112 - CURVE_H), fontSize: 10, color: '#2fb8b0', writingMode: 'vertical-rl', transform: 'rotate(180deg)' }}>
        OW Completion
      </div>
      {state.events.map((event, i) => {
        const lane = MARKER_LANES[i % MARKER_LANES.length]
        const y = BASE_Y - progressAt(event.t) + lane - 14
        return (
          <div
            key={`${event.t}-${i}`}
            style={{ ...at(xAt(event.t) - 10, Math.max(TOP + 2, y)), display: 'flex', justifyContent: 'center' }}
            title={`${event.kind}: ${event.label}`}
          >
            <EventMarker event={event} />
          </div>
        )
      })}
    </>
  )
}
