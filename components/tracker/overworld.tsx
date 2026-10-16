'use client'

import type { ReactNode } from 'react'
import { DARK_X, INVENTORY, OW_H, OW_W, TILES, isAlwaysEmpty, type TileDef } from '@/lib/data'
import { useTracker } from '@/lib/tracker-state'
import { at } from './header'
import { usePicker, type PickerItem } from './picker'
import { ItemIcon, MapTile } from './sprite'

const GROUP_COLOR: Record<TileDef['group'], string> = {
  dungeon: '#ffff00',
  road: '#da70d6',
  cave: '#e6e6e6',
  shop: '#4aa3ff',
  secret: '#3cc878',
  other: '#b0b0b0',
}

function Swatch({ def, w = 15, h = 27 }: { def: TileDef; w?: number; h?: number }) {
  return (
    <div
      style={{
        width: w,
        height: h,
        background: GROUP_COLOR[def.group],
        color: '#000',
        fontSize: def.label.length > 1 ? 9 : 14,
        fontWeight: 700,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        overflow: 'hidden',
      }}
    >
      {def.label}
    </div>
  )
}

const tilePickerItems: PickerItem[] = TILES.filter((t) => t.id !== DARK_X).map((t) => ({
  id: t.id,
  title: t.name,
  group: t.group,
  node: <Swatch def={t} w={22} h={26} />,
}))

const GROUP_TITLE: Record<TileDef['group'], string> = {
  dungeon: 'Dungeons',
  road: 'Any Roads (warps)',
  cave: 'Sword caves',
  shop: 'Shops',
  secret: 'Money secrets',
  other: 'Other',
}
for (const item of tilePickerItems) {
  item.group = GROUP_TITLE[TILES[item.id].group]
}

function OverworldTile({ x, y, coords }: { x: number; y: number; coords: boolean }) {
  const { state, dispatch } = useTracker()
  const openPicker = usePicker()
  const index = y * OW_W + x
  const tile = state.tiles[index]
  const def = tile.marker >= 0 ? TILES[tile.marker] : null
  const isX = tile.marker === DARK_X
  const letter = 'ABCDEFGH'[y]

  let content: ReactNode
  if (state.quest === 'unknown') {
    content = (
      <div
        style={{
          width: 48,
          height: 33,
          background: (x + y) % 2 ? '#1c2b2b' : '#233636',
        }}
      />
    )
  } else {
    const map = state.quest === 'second' || (state.quest === 'mixed' && tile.screen === 1) ? 1 : 0
    content = <MapTile map={map} x={x} y={y} />
  }

  const highlight =
    state.highlightRemaining &&
    tile.marker < 0 &&
    (state.quest === 'unknown' || !isAlwaysEmpty(state.quest, x, y))

  return (
    <button
      type="button"
      aria-label={`Overworld screen ${letter}${x + 1}${def ? `: ${def.name}` : ''}`}
      title={`${letter}${x + 1}${def ? ` - ${def.name}` : ''}`}
      onClick={(e) => {
        if (e.shiftKey && state.quest === 'mixed') {
          dispatch({ type: 'setTileScreen', index, screen: tile.screen === 1 ? 0 : 1 })
          return
        }
        openPicker(e, {
          title: `Mark screen ${letter}${x + 1}`,
          items: tilePickerItems,
          columns: 9,
          onPick: (marker) => dispatch({ type: 'setTile', index, marker }),
        })
      }}
      onContextMenu={(e) => {
        e.preventDefault()
        dispatch({ type: 'setTile', index, marker: tile.marker >= 0 ? -1 : DARK_X })
      }}
      style={{
        ...at(x * 48, 150 + y * 33, 48, 33),
        padding: 0,
        border: 0,
        background: '#000',
        overflow: 'hidden',
        outline: highlight ? '2px solid #ffa500' : undefined,
        outlineOffset: -2,
        zIndex: highlight ? 1 : undefined,
      }}
    >
      {content}
      {isX && <div style={{ position: 'absolute', inset: 0, background: 'rgba(0,0,0,0.78)' }} />}
      {def && !isX && (
        <div style={{ position: 'absolute', left: 16, top: 3 }}>
          <Swatch def={def} />
        </div>
      )}
      {coords && (x === 0 || y === 0) && (
        <span
          style={{
            position: 'absolute',
            left: 1,
            top: 0,
            fontSize: 10,
            fontWeight: 700,
            color: '#ff0',
            textShadow: '0 0 2px #000, 0 0 2px #000',
          }}
        >
          {x === 0 && y === 0 ? `${letter}1` : x === 0 ? letter : x + 1}
        </span>
      )}
    </button>
  )
}

export function OverworldMap({ coords }: { coords: boolean }) {
  const tiles: ReactNode[] = []
  for (let y = 0; y < OW_H; y++) {
    for (let x = 0; x < OW_W; x++) {
      tiles.push(<OverworldTile key={`${x}-${y}`} x={x} y={y} coords={coords} />)
    }
  }
  return <>{tiles}</>
}

function LegendSwatch({ x, label, color, text, shape }: { x: number; label: string; color: string; text: string; shape?: 'diamond' }) {
  return (
    <>
      <div
        aria-hidden="true"
        style={{
          ...at(x, 420, 16, 24),
          background: shape === 'diamond' ? 'transparent' : color,
          border: shape === 'diamond' ? `2px solid ${color}` : 0,
          color: shape === 'diamond' ? color : '#000',
          fontWeight: 700,
          fontSize: 14,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        {text}
      </div>
      <div style={{ ...at(x + 21, 420), fontSize: 12, lineHeight: '13px', whiteSpace: 'pre-line' }}>{label}</div>
    </>
  )
}

export function Legend() {
  return (
    <>
      <div style={{ ...at(4, 420, 84), fontSize: 10, lineHeight: '13px', whiteSpace: 'pre', letterSpacing: -0.5 }}>
        {'The LEGEND\nof Z-Tracker:'}
      </div>
      <LegendSwatch x={88} color="#da70d6" text="1" label={'Any Road\n(Warp)'} />
      <LegendSwatch x={176} color="#ffff00" text="8" label={'Dungeon\n(Completed)'} />
      <LegendSwatch x={282} color="#fff" text="8" shape="diamond" label={'Recorder\nDestination'} />
      <LegendSwatch x={384} color="#4aa3ff" text="S" label={'Shop /\nsecret'} />
      <div
        aria-hidden="true"
        style={{ ...at(478, 418, 26, 26), border: '3px solid #22cc44', borderRadius: '50%' }}
      />
      <div style={{ ...at(510, 420), fontSize: 12, lineHeight: '13px', whiteSpace: 'pre-line' }}>
        {'Right-click:\nmark empty'}
      </div>
    </>
  )
}

export function ItemProgress() {
  const { state, dispatch } = useTracker()
  return (
    <>
      <div style={{ ...at(8, 456), fontSize: 12 }}>Item Progress</div>
      {INVENTORY.map((item, i) => {
        const level = state.inventory[item.key] ?? 0
        const max = item.levels.length - 1
        const shown = item.levels[Math.max(level, 1)]
        const set = (value: number) => dispatch({ type: 'setInventory', key: item.key, value })
        const label = `${item.name}: ${item.levels[level].name}`
        return (
          <button
            key={item.key}
            type="button"
            aria-label={label}
            title={label}
            onClick={() => set(level >= max ? 0 : level + 1)}
            onContextMenu={(e) => {
              e.preventDefault()
              set(level <= 0 ? max : level - 1)
            }}
            style={{
              ...at(112 + i * 28, 446, 28, 30),
              background: 'transparent',
              border: 0,
              padding: 0,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <ItemIcon code={shown.code} dim={level === 0} />
          </button>
        )
      })}
    </>
  )
}
