'use client'

import { GROUP_LABEL, TILES, type TileGroup } from '@/lib/data'
import { useTracker } from '@/lib/tracker-state'
import { Popover } from './popover'

const GROUPS: TileGroup[] = ['dungeon', 'road', 'cave', 'shop', 'secret', 'other']

interface TilePickerProps {
  index: number
  x: number
  y: number
  onClose: () => void
}

export function TilePicker({ index, x, y, onClose }: TilePickerProps) {
  const { state, dispatch } = useTracker()
  const current = state.tiles[index]

  const usage = new Map<number, number>()
  for (const t of state.tiles) usage.set(t.marker, (usage.get(t.marker) ?? 0) + 1)

  function choose(marker: number) {
    dispatch({ type: 'setTile', index, marker })
    onClose()
  }

  return (
    <Popover x={x - 150} y={y} onClose={onClose} label="Mark overworld screen">
      <div className="flex w-[300px] flex-col gap-2">
        {state.quest === 'mixed' && (
          <div className="flex items-center gap-1 text-[11px]">
            <span className="mr-1 text-muted">Screen is</span>
            {([0, 1, 2] as const).map((s) => (
              <button
                key={s}
                type="button"
                className="btn !px-2 !py-0.5"
                aria-pressed={current.screen === s}
                onClick={() => dispatch({ type: 'setTileScreen', index, screen: s })}
              >
                {s === 0 ? 'Unknown' : s === 1 ? '1st Q' : '2nd Q'}
              </button>
            ))}
          </div>
        )}

        {GROUPS.map((group) => (
          <fieldset key={group} className="min-w-0">
            <legend className="mb-1 text-[10px] uppercase tracking-wide text-muted">
              {GROUP_LABEL[group]}
            </legend>
            <div className="flex flex-wrap gap-1">
              {TILES.filter((t) => t.group === group).map((t) => {
                const used = usage.get(t.id) ?? 0
                const isCurrent = current.marker === t.id
                const disabled = !isCurrent && used >= t.max
                return (
                  <button
                    key={t.id}
                    type="button"
                    title={t.name}
                    disabled={disabled}
                    aria-pressed={isCurrent}
                    onClick={() => choose(t.id)}
                    className="btn !min-w-9 !px-1.5 !py-1 font-pixel !text-[9px] disabled:cursor-not-allowed disabled:opacity-30"
                  >
                    {t.label}
                  </button>
                )
              })}
            </div>
          </fieldset>
        ))}

        <button
          type="button"
          className="btn hover:!border-bad hover:!text-bad"
          disabled={current.marker < 0}
          onClick={() => choose(-1)}
        >
          Clear screen
        </button>
      </div>
    </Popover>
  )
}
