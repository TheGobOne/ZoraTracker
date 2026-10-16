'use client'

import { Fragment, useState } from 'react'
import { DOORS, DUNGEON_SIZE, ROOMS } from '@/lib/data'
import { useTracker } from '@/lib/tracker-state'

const N = DUNGEON_SIZE

export function DungeonTracker() {
  const { state, dispatch } = useTracker()
  const [level, setLevel] = useState(0)
  const [brush, setBrush] = useState(1)

  const rooms = state.rooms[level]
  const hDoors = state.hDoors[level]
  const vDoors = state.vDoors[level]

  function paint(index: number) {
    dispatch({
      type: 'setRoom',
      level,
      index,
      value: rooms[index] === brush ? 0 : brush,
    })
  }

  function cycleDoor(axis: 'h' | 'v', index: number, delta: 1 | -1) {
    const list = axis === 'h' ? hDoors : vDoors
    const next = (list[index] + delta + DOORS.length) % DOORS.length
    dispatch({ type: 'setDoor', level, axis, index, value: next })
  }

  return (
    <section className="panel p-3" aria-labelledby="dungeon-title">
      <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
        <h2 id="dungeon-title" className="section-title">
          Dungeon map
        </h2>
        <button
          type="button"
          className="btn hover:!border-bad hover:!text-bad"
          onClick={() => {
            if (window.confirm(`Clear the map for level ${level + 1}?`)) {
              dispatch({ type: 'clearDungeon', level })
            }
          }}
        >
          Clear level {level + 1}
        </button>
      </div>

      <div role="tablist" aria-label="Dungeon level" className="mb-3 flex flex-wrap gap-1">
        {Array.from({ length: 9 }, (_, i) => (
          <button
            key={i}
            type="button"
            role="tab"
            aria-selected={level === i}
            onClick={() => setLevel(i)}
            className={`min-w-9 border px-2 py-1 font-pixel text-[10px] ${
              level === i
                ? 'border-accent bg-[#231a08] text-accent'
                : 'border-line bg-panel-2 text-muted hover:text-foreground'
            }`}
          >
            {i + 1}
          </button>
        ))}
      </div>

      <div className="flex flex-col gap-3 md:flex-row">
        <div
          className="mx-auto grid w-full max-w-[420px] shrink-0 border border-line bg-black p-1"
          style={{
            gridTemplateColumns: `repeat(${N * 2 - 1}, ${gapSize(0)})`,
            gridTemplateRows: `repeat(${N * 2 - 1}, ${gapSize(0)})`,
          }}
          role="grid"
          aria-label={`Level ${level + 1} room grid`}
        >
          {Array.from({ length: N * 2 - 1 }, (_, row) => (
            <Fragment key={row}>
              {Array.from({ length: N * 2 - 1 }, (_, col) => {
                const roomRow = row / 2
                const roomCol = col / 2
                const rowIsRoom = row % 2 === 0
                const colIsRoom = col % 2 === 0

                if (rowIsRoom && colIsRoom) {
                  const index = roomRow * N + roomCol
                  const room = ROOMS[rooms[index]]
                  return (
                    <button
                      key={col}
                      type="button"
                      role="gridcell"
                      title={room.name}
                      aria-label={`Room ${roomCol + 1}, ${roomRow + 1}: ${room.name}`}
                      onClick={() => paint(index)}
                      onContextMenu={(e) => {
                        e.preventDefault()
                        dispatch({ type: 'setRoom', level, index, value: 0 })
                      }}
                      className="flex aspect-square items-center justify-center border font-pixel text-[9px] leading-none hover:brightness-125"
                      style={{
                        background: rooms[index] === 0 ? '#0d0f0a' : room.id === 13 ? '#1a1a1a' : `${room.color}33`,
                        borderColor: rooms[index] === 0 ? '#1f2417' : room.color,
                        color: room.color,
                      }}
                    >
                      {room.code}
                    </button>
                  )
                }

                if (rowIsRoom && !colIsRoom) {
                  const index = roomRow * (N - 1) + (col - 1) / 2
                  return (
                    <DoorButton
                      key={col}
                      value={hDoors[index]}
                      orientation="h"
                      onCycle={(d) => cycleDoor('h', index, d)}
                    />
                  )
                }

                if (!rowIsRoom && colIsRoom) {
                  const index = ((row - 1) / 2) * N + roomCol
                  return (
                    <DoorButton
                      key={col}
                      value={vDoors[index]}
                      orientation="v"
                      onCycle={(d) => cycleDoor('v', index, d)}
                    />
                  )
                }

                return <span key={col} aria-hidden="true" />
              })}
            </Fragment>
          ))}
        </div>

        <div className="flex min-w-0 flex-1 flex-col gap-3">
          <fieldset>
            <legend className="mb-1 text-[10px] uppercase tracking-wide text-muted">
              Room brush
            </legend>
            <div className="grid grid-cols-2 gap-1">
              {ROOMS.filter((r) => r.id !== 0).map((r) => (
                <button
                  key={r.id}
                  type="button"
                  aria-pressed={brush === r.id}
                  onClick={() => setBrush(r.id)}
                  className="btn !justify-start !px-1.5 !py-1 !text-[11px]"
                >
                  <span
                    className="inline-flex size-5 shrink-0 items-center justify-center border font-pixel text-[8px]"
                    style={{ borderColor: r.color, color: r.color, background: `${r.color}33` }}
                  >
                    {r.code}
                  </span>
                  <span className="truncate">{r.name}</span>
                </button>
              ))}
            </div>
          </fieldset>

          <div>
            <p className="mb-1 text-[10px] uppercase tracking-wide text-muted">Doors</p>
            <ul className="flex flex-wrap gap-x-3 gap-y-1 text-[11px] text-muted">
              {DOORS.filter((d) => d.id !== 0).map((d) => (
                <li key={d.id} className="flex items-center gap-1.5">
                  <span className="inline-block h-2 w-4" style={{ background: d.color }} />
                  {d.name}
                </li>
              ))}
            </ul>
          </div>

          <p className="text-[11px] leading-relaxed text-muted">
            Click a room to paint it with the selected brush; click again to erase. Right-click
            clears. Click the gaps between rooms to cycle door types (right-click goes backward).
          </p>
        </div>
      </div>
    </section>
  )
}

// Rooms are 1fr wide; the door gaps between them are thin.
function gapSize(i: number): string {
  return i % 2 === 0 ? 'minmax(0, 1fr)' : '10px'
}

function DoorButton({
  value,
  orientation,
  onCycle,
}: {
  value: number
  orientation: 'h' | 'v'
  onCycle: (delta: 1 | -1) => void
}) {
  const door = DOORS[value]
  return (
    <button
      type="button"
      role="gridcell"
      title={door.name}
      aria-label={`${orientation === 'h' ? 'Horizontal' : 'Vertical'} door: ${door.name}`}
      onClick={() => onCycle(1)}
      onContextMenu={(e) => {
        e.preventDefault()
        onCycle(-1)
      }}
      className="group flex items-center justify-center"
    >
      <span
        className={`block transition-transform group-hover:scale-125 ${
          orientation === 'h' ? 'h-1.5 w-full' : 'h-full w-1.5'
        }`}
        style={{ background: value === 0 ? '#1f2417' : door.color }}
      />
    </button>
  )
}
