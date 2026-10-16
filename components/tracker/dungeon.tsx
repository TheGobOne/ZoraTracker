'use client'

import { useState, type ReactNode } from 'react'
import { DOORS, DUNGEON_SIZE, ROOMS, type Quest } from '@/lib/data'
import { useTracker } from '@/lib/tracker-state'
import { at } from './header'
import { usePicker, type PickerItem } from './picker'

const N = DUNGEON_SIZE
const GRID_X = 8
const GRID_Y = 538
const STEP_X = 51
const STEP_Y = 39

const roomPickerItems: PickerItem[] = ROOMS.map((r) => ({
  id: r.id,
  title: r.name,
  node: (
    <div
      style={{
        width: 39,
        height: 27,
        background: r.id === 0 ? '#05051a' : r.color,
        border: r.id === 0 ? '2px solid #26267a' : '2px solid #000',
        color: '#000',
        fontSize: 13,
        fontWeight: 700,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      {r.code}
    </div>
  ),
}))

function Room({ level, row, col }: { level: number; row: number; col: number }) {
  const { state, dispatch } = useTracker()
  const openPicker = usePicker()
  const index = row * N + col
  const value = state.rooms[level][index]
  const def = ROOMS[value]
  const name = `Room row ${row + 1}, column ${col + 1}`

  let style: React.CSSProperties
  if (value === 0) {
    style = { background: '#05051a', boxShadow: 'inset 0 0 0 2px #26267a' }
  } else if (value === 13) {
    style = { background: '#000', boxShadow: 'none' }
  } else {
    style = { background: def.color, boxShadow: 'inset 0 0 0 2px rgba(0,0,0,0.55)' }
  }

  return (
    <button
      type="button"
      aria-label={`${name}: ${def.name}`}
      title={`${name}: ${def.name}`}
      onClick={(e) =>
        openPicker(e, {
          title: name,
          items: roomPickerItems,
          columns: 7,
          onPick: (v) => dispatch({ type: 'setRoom', level, index, value: v }),
        })
      }
      onContextMenu={(e) => {
        e.preventDefault()
        dispatch({ type: 'setRoom', level, index, value: 0 })
      }}
      style={{
        ...at(GRID_X + col * STEP_X, GRID_Y + row * STEP_Y, 39, 27),
        border: 0,
        padding: 0,
        color: '#000',
        fontSize: 14,
        fontWeight: 700,
        ...style,
      }}
    >
      {def.code}
    </button>
  )
}

function Door({ level, axis, index }: { level: number; axis: 'h' | 'v'; index: number }) {
  const { state, dispatch } = useTracker()
  const value = (axis === 'h' ? state.hDoors : state.vDoors)[level][index]
  const def = DOORS[value]
  const cycle = (dir: 1 | -1) =>
    dispatch({ type: 'setDoor', level, axis, index, value: (value + dir + DOORS.length) % DOORS.length })

  let left: number
  let top: number
  let w: number
  let h: number
  if (axis === 'h') {
    const row = Math.floor(index / (N - 1))
    const col = index % (N - 1)
    left = GRID_X + col * STEP_X + 39
    top = GRID_Y + row * STEP_Y
    w = 12
    h = 27
  } else {
    const gap = Math.floor(index / N)
    const col = index % N
    left = GRID_X + col * STEP_X
    top = GRID_Y + gap * STEP_Y + 27
    w = 39
    h = 12
  }
  const barW = axis === 'h' ? 12 : 8
  const barH = axis === 'h' ? 8 : 12
  const unknown = value === 0

  return (
    <button
      type="button"
      aria-label={`${axis === 'h' ? 'Horizontal' : 'Vertical'} door ${index + 1}: ${def.name}`}
      title={def.name}
      onClick={() => cycle(1)}
      onContextMenu={(e) => {
        e.preventDefault()
        cycle(-1)
      }}
      style={{
        ...at(left, top, w, h),
        background: 'transparent',
        border: 0,
        padding: 0,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <div
        style={{
          width: barW,
          height: barH,
          background: unknown ? '#16164a' : def.color,
        }}
      />
    </button>
  )
}

const QUESTS: { quest: Quest; label: string; title: string }[] = [
  { quest: 'first', label: 'FQ', title: 'First Quest map' },
  { quest: 'second', label: 'SQ', title: 'Second Quest map' },
  { quest: 'mixed', label: 'MQ', title: 'Mixed Quest (shift-click a screen to flip it)' },
]

export function DungeonTracker() {
  const { state, dispatch } = useTracker()
  const [level, setLevel] = useState(0)

  const cells: ReactNode[] = []
  for (let row = 0; row < N; row++) {
    for (let col = 0; col < N; col++) {
      cells.push(<Room key={`r${row}-${col}`} level={level} row={row} col={col} />)
      if (col < N - 1) cells.push(<Door key={`h${row}-${col}`} level={level} axis="h" index={row * (N - 1) + col} />)
      if (row < N - 1) cells.push(<Door key={`v${row}-${col}`} level={level} axis="v" index={row * N + col} />)
    }
  }

  return (
    <>
      <div role="tablist" aria-label="Dungeon level">
        {Array.from({ length: 9 }, (_, i) => (
          <button
            key={i}
            type="button"
            role="tab"
            aria-selected={level === i}
            onClick={() => setLevel(i)}
            style={{
              ...at(8 + i * 38, 478, 35, 22),
              background: level === i ? '#fff' : '#111',
              color: level === i ? '#000' : '#ccc',
              border: '1px solid #888',
              padding: 0,
              fontSize: 13,
            }}
          >
            {i + 1}
          </button>
        ))}
      </div>
      {QUESTS.map((q, i) => (
        <button
          key={q.quest}
          type="button"
          title={q.title}
          aria-pressed={state.quest === q.quest}
          onClick={() => dispatch({ type: 'setQuest', quest: q.quest })}
          style={{
            ...at(356 + i * 32, 478, 30, 22),
            background: state.quest === q.quest ? '#ffa500' : '#111',
            color: state.quest === q.quest ? '#000' : '#ffa500',
            border: '1px solid #888',
            padding: 0,
            fontSize: 12,
          }}
        >
          {q.label}
        </button>
      ))}
      <div
        style={{
          ...at(8, 506),
          fontFamily: 'var(--font-pixel)',
          fontSize: 20,
          letterSpacing: 10,
          color: '#fff',
          whiteSpace: 'nowrap',
        }}
      >
        {`LEVEL-${level + 1}`}
      </div>
      <button
        type="button"
        className="orange-btn"
        style={at(340, 508, 62, 20)}
        onClick={() => dispatch({ type: 'clearDungeon', level })}
      >
        Clear map
      </button>
      {cells}
    </>
  )
}
