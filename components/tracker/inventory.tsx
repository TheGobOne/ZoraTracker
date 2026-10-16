'use client'

import { INVENTORY } from '@/lib/data'
import { useTracker } from '@/lib/tracker-state'
import { Chip, Sprite } from './chip'

export function Inventory() {
  const { state, dispatch } = useTracker()

  return (
    <section className="panel p-3" aria-labelledby="inventory-title">
      <h2 id="inventory-title" className="section-title mb-2">
        Inventory
      </h2>
      <ul className="grid grid-cols-4 gap-2 sm:grid-cols-6 lg:grid-cols-8">
        {INVENTORY.map((def) => {
          const value = state.inventory[def.key] ?? 0
          const level = def.levels[value]
          const max = def.levels.length - 1
          return (
            <li key={def.key}>
              <button
                type="button"
                title={`${level.name} (click to upgrade, right-click to downgrade)`}
                aria-label={`${def.name}: ${level.name}`}
                onClick={() =>
                  dispatch({
                    type: 'setInventory',
                    key: def.key,
                    value: value >= max ? 0 : value + 1,
                  })
                }
                onContextMenu={(e) => {
                  e.preventDefault()
                  dispatch({ type: 'setInventory', key: def.key, value: Math.max(0, value - 1) })
                }}
                className="flex w-full flex-col items-center gap-1 border border-line bg-panel-2 p-1.5 hover:border-accent"
              >
                <Chip code={level.code} color={level.color} dim={value === 0} size={30} />
                <span className="w-full truncate text-center text-[10px] text-muted">{def.name}</span>
              </button>
            </li>
          )
        })}
        <li>
          <div className="flex h-full flex-col items-center justify-between gap-1 border border-line bg-panel-2 p-1.5">
            <Sprite sheet="items" index={1} size={30} />
            <div className="flex items-center gap-1">
              <button
                type="button"
                className="btn !px-1.5 !py-0"
                aria-label="Remove a bonus heart"
                onClick={() => dispatch({ type: 'setBonusHearts', value: state.bonusHearts - 1 })}
              >
                -
              </button>
              <span className="min-w-5 text-center font-pixel text-[10px] text-gold">
                {state.bonusHearts}
              </span>
              <button
                type="button"
                className="btn !px-1.5 !py-0"
                aria-label="Add a bonus heart"
                onClick={() => dispatch({ type: 'setBonusHearts', value: state.bonusHearts + 1 })}
              >
                +
              </button>
            </div>
            <span className="text-[10px] text-muted">OW hearts</span>
          </div>
        </li>
      </ul>
    </section>
  )
}
