'use client'

import type { CSSProperties, ReactNode } from 'react'
import {
  BOX_ITEMS,
  MAX_SPOTS,
  isAlwaysEmpty,
  isSometimesEmpty,
  OW_H,
  OW_W,
} from '@/lib/data'
import { formatTime, maxHearts, useElapsed, useTracker } from '@/lib/tracker-state'
import { usePicker, type PickerItem } from './picker'
import { ItemIcon, Sprite } from './sprite'

export const at = (x: number, y: number, w?: number, h?: number): CSSProperties => ({
  position: 'absolute',
  left: x,
  top: y,
  width: w,
  height: h,
})

const RED = '#c81e1e'
const GREEN = '#1ec81e'

const boxPickerItems: PickerItem[] = BOX_ITEMS.map((i) => ({
  id: i.id,
  title: i.name,
  node: <ItemIcon code={i.code} />,
}))

function ItemBox({
  x,
  y,
  value,
  label,
  onPick,
  onClear,
}: {
  x: number
  y: number
  value: number
  label: string
  onPick: (id: number) => void
  onClear: () => void
}) {
  const openPicker = usePicker()
  const item = BOX_ITEMS.find((i) => i.id === value)
  return (
    <button
      type="button"
      aria-label={item ? `${label}: ${item.name}` : `${label}: empty`}
      title={item ? item.name : `${label} (click to assign, right-click to clear)`}
      onClick={(e) =>
        openPicker(e, { title: `${label} item`, items: boxPickerItems, columns: 8, onPick })
      }
      onContextMenu={(e) => {
        e.preventDefault()
        onClear()
      }}
      style={{
        ...at(x, y, 30, 30),
        background: '#000',
        border: 0,
        padding: 0,
        boxShadow: `inset 0 0 0 2px ${item ? GREEN : RED}`,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      {item && <ItemIcon code={item.code} />}
    </button>
  )
}

function DungeonItemGrid() {
  const { state, dispatch } = useTracker()
  const cells: ReactNode[] = []

  for (let level = 0; level < 9; level++) {
    const x = level * 30
    if (level < 8) {
      const got = state.triforces[level]
      cells.push(
        <button
          key={`tf${level}`}
          type="button"
          aria-pressed={got}
          aria-label={`Triforce piece ${level + 1}`}
          onClick={() => dispatch({ type: 'toggleTriforce', level })}
          style={{ ...at(x, 0, 30, 30), background: '#000', border: 0, padding: 0 }}
        >
          <Sprite sheet="icons10" index={got ? 1 : 0} />
          <span
            style={{
              position: 'absolute',
              left: 0,
              right: 0,
              bottom: 4,
              textAlign: 'center',
              fontSize: 12,
              fontWeight: 700,
              color: got ? '#fff' : '#77779a',
            }}
          >
            {level + 1}
          </span>
        </button>,
      )
    } else {
      cells.push(
        <div
          key="nine"
          style={{
            ...at(x, 0, 30, 30),
            color: '#fff',
            fontWeight: 700,
            fontSize: 16,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          9
        </div>,
      )
    }
    for (let slot = 0; slot < 3; slot++) {
      cells.push(
        <ItemBox
          key={`b${level}-${slot}`}
          x={x}
          y={30 + slot * 30}
          value={state.boxes[level][slot]}
          label={`Level ${level + 1} item ${slot + 1}`}
          onPick={(item) => dispatch({ type: 'setBox', level, slot, item })}
          onClear={() => dispatch({ type: 'setBox', level, slot, item: 0 })}
        />,
      )
    }
  }
  return <>{cells}</>
}

/** A 30x30 cell in the 6x4 grid of non-dungeon items that starts at (280, 30). */
function GridToggle({
  col,
  row,
  invKey,
  code,
  label,
}: {
  col: number
  row: number
  invKey: string
  code: string
  label: string
}) {
  const { state, dispatch } = useTracker()
  const on = (state.inventory[invKey] ?? 0) > 0
  return (
    <button
      type="button"
      aria-pressed={on}
      aria-label={label}
      title={label}
      onClick={() => dispatch({ type: 'setInventory', key: invKey, value: on ? 0 : 1 })}
      style={{
        ...at(280 + col * 30, 30 + row * 30, 30, 30),
        background: 'transparent',
        border: 0,
        padding: 0,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <ItemIcon code={code} dim={!on} />
    </button>
  )
}

function GridBox({ col, row, invKey, label }: { col: number; row: number; invKey: string; label: string }) {
  const { state, dispatch } = useTracker()
  return (
    <ItemBox
      x={280 + col * 30}
      y={30 + row * 30}
      value={state.inventory[invKey] ?? 0}
      label={label}
      onPick={(value) => dispatch({ type: 'setInventory', key: invKey, value })}
      onClear={() => dispatch({ type: 'setInventory', key: invKey, value: 0 })}
    />
  )
}

function Decor({ col, row, index, label }: { col: number; row: number; index: number; label: string }) {
  return (
    <div
      role="img"
      aria-label={label}
      style={{
        ...at(280 + col * 30, 30 + row * 30, 30, 30),
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <Sprite sheet="icons7" index={index} />
    </div>
  )
}

function OtherItemGrid() {
  const { state, dispatch } = useTracker()
  const taken = state.bonusHearts
  return (
    <>
      <Decor col={0} row={0} index={17} label="White sword" />
      <GridBox col={1} row={0} invKey="box:ws" label="White sword cave item" />
      <GridToggle col={2} row={0} invKey="g:mags" code="MS" label="Magical sword" />
      <GridToggle col={3} row={0} invKey="g:wood" code="WD" label="Wooden sword" />
      <GridToggle col={4} row={0} invKey="g:bluering" code="BR" label="Blue ring" />
      <GridToggle col={5} row={0} invKey="g:boombook" code="BK" label="Boomerang and book shop" />

      <Decor col={0} row={1} index={4} label="Ladder" />
      <GridBox col={1} row={1} invKey="box:ladder" label="Coast item" />
      <GridToggle col={2} row={1} invKey="g:bluecandle" code="BC" label="Blue candle" />
      <GridToggle col={3} row={1} invKey="g:woodarrow" code="AR" label="Wooden arrows" />
      <GridToggle col={4} row={1} invKey="g:rupee" code="MK" label="Rupee shop" />
      <GridToggle col={5} row={1} invKey="g:bomb" code="BM" label="Bombs" />

      <Decor col={0} row={2} index={18} label="Armos" />
      <GridBox col={1} row={2} invKey="box:armos" label="Armos item" />
      <GridToggle col={2} row={2} invKey="g:ganon" code="GA" label="Killed Ganon" />
      <GridToggle col={3} row={2} invKey="g:zelda" code="ZE" label="Rescued Zelda" />

      {[0, 1, 2, 3].map((i) => (
        <button
          key={i}
          type="button"
          aria-pressed={i < taken}
          aria-label={`Take-any heart ${i + 1}`}
          title="Take-any heart"
          onClick={() => dispatch({ type: 'setBonusHearts', value: i < taken ? i : i + 1 })}
          style={{
            ...at(280 + i * 30, 120, 30, 30),
            background: 'transparent',
            border: 0,
            padding: 0,
          }}
        >
          <Sprite sheet="icons10" index={i < taken ? 4 : 3} />
        </button>
      ))}
    </>
  )
}

function InfoText({ coords, onCoords }: { coords: boolean; onCoords: (v: boolean) => void }) {
  const { state, dispatch } = useTracker()
  let open = 0
  let gettable = 0
  for (let y = 0; y < OW_H; y++) {
    for (let x = 0; x < OW_W; x++) {
      if (state.tiles[y * OW_W + x].marker >= 0) continue
      if (state.quest === 'unknown') {
        open++
        gettable++
        continue
      }
      if (isAlwaysEmpty(state.quest, x, y)) continue
      open++
      if (!isSometimesEmpty(state.quest, x, y)) gettable++
    }
  }
  const remaining = state.quest === 'unknown' ? open : Math.min(open, MAX_SPOTS[state.quest])

  const checkbox = (checked: boolean, onChange: () => void, text: string, x: number, y: number) => (
    <label style={{ ...at(x, y), display: 'flex', alignItems: 'center', gap: 4, fontSize: 13, whiteSpace: 'nowrap' }}>
      <input
        type="checkbox"
        checked={checked}
        onChange={onChange}
        style={{ width: 12, height: 12, accentColor: '#ffa500' }}
      />
      {text}
    </label>
  )

  return (
    <>
      <div style={{ ...at(402, 94), fontSize: 13, whiteSpace: 'nowrap' }}>{remaining} OW spots left</div>
      {checkbox(state.highlightRemaining, () => dispatch({ type: 'toggleHighlight' }), `${gettable} gettable`, 402, 112)}
      <div
        style={{
          ...at(412, 128, 88, 18),
          border: '1px solid #888',
          fontSize: 12,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          whiteSpace: 'nowrap',
        }}
      >
        Max Hearts: {maxHearts(state)}
      </div>
      {checkbox(coords, () => onCoords(!coords), 'Coords', 490, 60)}
    </>
  )
}

function TimerPanel() {
  const { state, dispatch } = useTracker()
  const elapsed = useElapsed()
  const running = state.timer.startedAt !== null
  return (
    <>
      <div
        role="timer"
        aria-label="Run timer"
        style={{
          ...at(608, 4, 160, 48),
          color: '#5dff5d',
          fontFamily: 'Arial, Helvetica, sans-serif',
          fontSize: 38,
          fontWeight: 400,
          letterSpacing: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontVariantNumeric: 'tabular-nums',
        }}
      >
        {formatTime(elapsed)}
      </div>
      <button
        type="button"
        className="orange-btn"
        style={at(610, 62, 100, 22)}
        onClick={() => dispatch({ type: 'toggleTimer' })}
      >
        {running ? 'Pause timer' : elapsed > 0 ? 'Resume timer' : 'Start timer'}
      </button>
      <button
        type="button"
        className="orange-btn"
        style={at(714, 62, 50, 22)}
        onClick={() => dispatch({ type: 'resetTimer' })}
      >
        Reset
      </button>
      <div style={{ ...at(612, 94, 108), fontSize: 11, color: '#c8c8c8', lineHeight: '12px' }}>
        Timeline marks use this clock.
      </div>
      <div style={at(724, 112)} role="img" aria-label="Link">
        <Sprite sheet="link" index={0} scale={2} />
      </div>
    </>
  )
}

export function Header({ coords, onCoords }: { coords: boolean; onCoords: (v: boolean) => void }) {
  return (
    <>
      <DungeonItemGrid />
      <OtherItemGrid />
      <InfoText coords={coords} onCoords={onCoords} />
      <TimerPanel />
    </>
  )
}
