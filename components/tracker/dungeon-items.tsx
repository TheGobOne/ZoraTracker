'use client'

import { useState } from 'react'
import { BOX_ITEMS } from '@/lib/data'
import { useTracker } from '@/lib/tracker-state'
import { Chip, Sprite } from './chip'
import { Popover } from './popover'

const LEVELS = [0, 1, 2, 3, 4, 5, 6, 7, 8]

export function DungeonItems() {
  const { state, dispatch } = useTracker()
  const [picker, setPicker] = useState<{ level: number; slot: number; x: number; y: number } | null>(
    null,
  )

  return (
    <section className="panel p-3" aria-labelledby="dungeon-items-title">
      <h2 id="dungeon-items-title" className="section-title mb-2">
        Dungeon items
      </h2>
      <div className="grid grid-cols-3 gap-2 sm:grid-cols-5 lg:grid-cols-9">
        {LEVELS.map((level) => {
          const isNine = level === 8
          const hasTriforce = !isNine && state.triforces[level]
          return (
            <div key={level} className="flex flex-col items-center gap-1.5 border border-line bg-panel-2 p-2">
              <div className="flex w-full items-center justify-between">
                <span className="font-pixel text-[10px] text-gold">{level + 1}</span>
                {!isNine && (
                  <button
                    type="button"
                    onClick={() => dispatch({ type: 'toggleTriforce', level })}
                    aria-pressed={hasTriforce}
                    aria-label={`Triforce piece ${level + 1}`}
                    className="p-0.5 hover:bg-line"
                  >
                    <Sprite sheet="items" index={0} size={24} dim={!hasTriforce} />
                  </button>
                )}
              </div>
              <div className="flex gap-1">
                {state.boxes[level].map((itemId, slot) => {
                  const item = BOX_ITEMS.find((i) => i.id === itemId)
                  return (
                    <button
                      key={slot}
                      type="button"
                      aria-label={`Level ${level + 1} item box ${slot + 1}: ${item?.name ?? 'empty'}`}
                      title={item?.name ?? 'Set item'}
                      onClick={(e) => {
                        const r = e.currentTarget.getBoundingClientRect()
                        setPicker({ level, slot, x: r.left, y: r.bottom + 4 })
                      }}
                      onContextMenu={(e) => {
                        e.preventDefault()
                        dispatch({ type: 'setBox', level, slot, item: 0 })
                      }}
                      className="hover:brightness-125"
                    >
                      {item ? (
                        <Chip code={item.code} color={item.color} size={26} />
                      ) : (
                        <Chip code="" color="#2b3220" size={26} dim />
                      )}
                    </button>
                  )
                })}
              </div>
            </div>
          )
        })}
      </div>
      <p className="mt-2 text-[11px] text-muted">
        Click a box to assign an item, right-click to clear. Heart containers raise your max hearts.
      </p>

      {picker && (
        <Popover x={picker.x} y={picker.y} onClose={() => setPicker(null)} label="Choose item">
          <div className="grid w-[260px] grid-cols-4 gap-1">
            {BOX_ITEMS.map((item) => (
              <button
                key={item.id}
                type="button"
                title={item.name}
                onClick={() => {
                  dispatch({ type: 'setBox', level: picker.level, slot: picker.slot, item: item.id })
                  setPicker(null)
                }}
                className="flex flex-col items-center gap-1 border border-transparent p-1 text-[9px] text-muted hover:border-accent hover:text-foreground"
              >
                <Chip code={item.code} color={item.color} />
                <span className="w-full truncate text-center">{item.name}</span>
              </button>
            ))}
            <button
              type="button"
              onClick={() => {
                dispatch({ type: 'setBox', level: picker.level, slot: picker.slot, item: 0 })
                setPicker(null)
              }}
              className="col-span-4 border border-line p-1 text-[11px] text-muted hover:border-bad hover:text-bad"
            >
              Clear box
            </button>
          </div>
        </Popover>
      )}
    </section>
  )
}
