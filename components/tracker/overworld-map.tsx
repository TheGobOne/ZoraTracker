'use client'

import { useState } from 'react'
import {
  DARK_X,
  MAX_SPOTS,
  OW_H,
  OW_W,
  TILES,
  isAlwaysEmpty,
  isSometimesEmpty,
  type Quest,
  type TileDef,
  type TileGroup,
} from '@/lib/data'
import { spotsMarked, useTracker, type OwTile } from '@/lib/tracker-state'
import { TilePicker } from './tile-picker'

const STRIP_MAPS = 5
const GROUP_STYLE: Record<TileGroup, { bg: string; fg: string }> = {
  dungeon: { bg: '#f2c94c', fg: '#0a0c08' },
  road: { bg: '#e879b0', fg: '#0a0c08' },
  cave: { bg: '#5b8def', fg: '#0a0c08' },
  shop: { bg: '#ef8300', fg: '#0a0c08' },
  secret: { bg: '#4ade80', fg: '#0a0c08' },
  other: { bg: '#cfd6e3', fg: '#0a0c08' },
}

// Which of the five 256px maps in the sprite strip to draw for a tile.
function mapIndex(quest: Quest, tile: OwTile): number {
  if (quest === 'first') return 0
  if (quest === 'second') return 1
  if (quest === 'mixed') return tile.screen === 1 ? 2 : tile.screen === 2 ? 3 : 4
  return 4
}

export function OverworldMap() {
  const { state, dispatch } = useTracker()
  const [picker, setPicker] = useState<{ index: number; x: number; y: number } | null>(null)

  const remaining = Math.max(0, MAX_SPOTS[state.quest] - spotsMarked(state))
  const totalWidthTiles = STRIP_MAPS * OW_W

  return (
    <section className="panel p-3" aria-labelledby="ow-title">
      <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
        <h2 id="ow-title" className="section-title">
          Overworld
        </h2>
        <div className="flex items-center gap-3 text-[12px]">
          <span className="text-accent">
            {remaining} spot{remaining === 1 ? '' : 's'} left
          </span>
          <button
            type="button"
            className="btn"
            aria-pressed={state.highlightRemaining}
            onClick={() => dispatch({ type: 'toggleHighlight' })}
          >
            Highlight spots
          </button>
        </div>
      </div>

      <div
        className="grid w-full border border-line"
        style={{ gridTemplateColumns: `repeat(${OW_W}, minmax(0, 1fr))` }}
      >
        {Array.from({ length: OW_W * OW_H }, (_, index) => {
          const x = index % OW_W
          const y = Math.floor(index / OW_W)
          const tile = state.tiles[index]
          const def: TileDef | undefined = TILES.find((t) => t.id === tile.marker)
          const empty = isAlwaysEmpty(state.quest, x, y)
          const maybeEmpty = isSometimesEmpty(state.quest, x, y)
          const marked = tile.marker >= 0
          const isDark = tile.marker === DARK_X
          const highlighted = state.highlightRemaining && !marked && !empty
          const n = mapIndex(state.quest, tile)

          return (
            <button
              key={index}
              type="button"
              aria-label={`Overworld screen column ${x + 1}, row ${y + 1}${def ? `: ${def.name}` : empty ? ': always empty' : ''}`}
              onClick={(e) => {
                const r = e.currentTarget.getBoundingClientRect()
                setPicker({ index, x: r.left + r.width / 2, y: r.bottom + 4 })
              }}
              onContextMenu={(e) => {
                e.preventDefault()
                if (marked) dispatch({ type: 'setTile', index, marker: -1 })
                else dispatch({ type: 'setTile', index, marker: DARK_X })
              }}
              className="pixelated relative aspect-[16/11] w-full outline-offset-[-2px] hover:z-10 hover:outline hover:outline-2 hover:outline-accent"
              style={{
                backgroundImage: 'url(/sprites/s_map_overworld_vanilla_strip8.png)',
                backgroundSize: `${totalWidthTiles * 100}% ${OW_H * 100}%`,
                backgroundPosition: `${((n * OW_W + x) / (totalWidthTiles - 1)) * 100}% ${(y / (OW_H - 1)) * 100}%`,
                backgroundRepeat: 'no-repeat',
              }}
            >
              {(empty || isDark) && <span className="absolute inset-0 bg-black/60" />}
              {maybeEmpty && !marked && <span className="absolute inset-0 bg-black/25" />}
              {highlighted && (
                <span className="absolute inset-0 border-2 border-good bg-good/20" aria-hidden="true" />
              )}
              {def && !isDark && (
                <span
                  className="absolute left-1/2 top-1/2 flex -translate-x-1/2 -translate-y-1/2 items-center justify-center border border-black px-[3px] font-pixel text-[7px] leading-none shadow-[0_0_0_1px_rgba(0,0,0,0.6)] sm:text-[8px] lg:min-h-[18px] lg:min-w-[18px] lg:text-[9px]"
                  style={{
                    background: GROUP_STYLE[def.group].bg,
                    color: GROUP_STYLE[def.group].fg,
                    minHeight: 12,
                    minWidth: 12,
                    paddingBlock: 2,
                  }}
                >
                  {def.label}
                </span>
              )}
              {isDark && (
                <span className="absolute inset-0 flex items-center justify-center font-pixel text-[10px] text-bad">
                  {'\u00d7'}
                </span>
              )}
            </button>
          )
        })}
      </div>

      <Legend />

      {picker && (
        <TilePicker
          index={picker.index}
          x={picker.x}
          y={picker.y}
          onClose={() => setPicker(null)}
        />
      )}
    </section>
  )
}

function Legend() {
  const groups: { group: TileGroup; label: string }[] = [
    { group: 'dungeon', label: 'Dungeon' },
    { group: 'road', label: 'Any road' },
    { group: 'cave', label: 'Sword cave' },
    { group: 'shop', label: 'Shop' },
    { group: 'secret', label: 'Secret' },
    { group: 'other', label: 'Other' },
  ]
  return (
    <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-[11px] text-muted">
      {groups.map((g) => (
        <span key={g.group} className="flex items-center gap-1.5">
          <span
            className="inline-block size-2.5 border border-black"
            style={{ background: GROUP_STYLE[g.group].bg }}
          />
          {g.label}
        </span>
      ))}
      <span className="ml-auto">Click a screen to mark it, right-click to mark empty or clear.</span>
    </div>
  )
}
