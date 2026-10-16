'use client'

import { BLOCKERS } from '@/lib/data'
import { useTracker } from '@/lib/tracker-state'
import { Chip } from './chip'

export function Blockers() {
  const { state, dispatch } = useTracker()

  return (
    <section className="panel p-3" aria-labelledby="blockers-title">
      <h2 id="blockers-title" className="section-title mb-2">
        Blockers
      </h2>
      <ul className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        {state.blockers.map((slots, level) => (
          <li key={level} className="flex items-center gap-2 border border-line bg-panel-2 p-1.5">
            <span className="w-3 font-pixel text-[10px] text-gold">{level + 1}</span>
            <div className="flex gap-1">
              {slots.map((value, slot) => {
                const b = BLOCKERS[value]
                return (
                  <button
                    key={slot}
                    type="button"
                    title={b.name}
                    aria-label={`Level ${level + 1} blocker ${slot + 1}: ${b.name}`}
                    onClick={() =>
                      dispatch({
                        type: 'setBlocker',
                        level,
                        slot,
                        value: (value + 1) % BLOCKERS.length,
                      })
                    }
                    onContextMenu={(e) => {
                      e.preventDefault()
                      dispatch({
                        type: 'setBlocker',
                        level,
                        slot,
                        value: (value - 1 + BLOCKERS.length) % BLOCKERS.length,
                      })
                    }}
                    className="hover:brightness-125"
                  >
                    <Chip code={b.code} color={b.color} dim={value === 0} size={26} />
                  </button>
                )
              })}
            </div>
          </li>
        ))}
      </ul>
      <p className="mt-2 text-[11px] text-muted">
        Note what stops you from clearing each dungeon. Click to cycle, right-click to go back.
      </p>
    </section>
  )
}
