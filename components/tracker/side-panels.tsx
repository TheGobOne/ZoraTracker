'use client'

import { useRef } from 'react'
import { BLOCKERS } from '@/lib/data'
import { parseSaved, serialize, useTracker } from '@/lib/tracker-state'
import { at } from './header'
import { usePicker, type PickerItem } from './picker'
import { ItemIcon } from './sprite'

const blockerItems: PickerItem[] = BLOCKERS.map((b, id) => ({
  id,
  title: b.name,
  node: b.code ? <ItemIcon code={b.code} /> : <span style={{ color: '#888', fontSize: 11 }}>none</span>,
}))

function BlockerBox({ level, slot, x, y }: { level: number; slot: number; x: number; y: number }) {
  const { state, dispatch } = useTracker()
  const openPicker = usePicker()
  const value = state.blockers[level][slot]
  const def = BLOCKERS[value]
  return (
    <button
      type="button"
      aria-label={`Level ${level + 1} blocker ${slot + 1}: ${def.name}`}
      title={def.name}
      onClick={(e) =>
        openPicker(e, {
          title: `Level ${level + 1} blocker`,
          items: blockerItems,
          columns: 6,
          onPick: (v) => dispatch({ type: 'setBlocker', level, slot, value: v }),
        })
      }
      onContextMenu={(e) => {
        e.preventDefault()
        dispatch({ type: 'setBlocker', level, slot, value: 0 })
      }}
      style={{
        ...at(x, y, 30, 28),
        background: '#000',
        border: 0,
        padding: 0,
        boxShadow: 'inset 0 0 0 2px #8a8a8a',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      {def.code && <ItemIcon code={def.code} />}
    </button>
  )
}

export function Blockers() {
  return (
    <>
      <div style={{ ...at(458, 480), fontSize: 11, color: '#ffa500', letterSpacing: 1 }}>BLOCKERS</div>
      {[0, 1].map((col) =>
        [0, 1, 2, 3].map((row) => {
          const level = col * 4 + row
          const x = 458 + col * 156
          const y = 494 + row * 30
          return (
            <div key={level}>
              <div style={{ ...at(x, y + 7), fontSize: 14, color: '#ffa500' }}>{level + 1}</div>
              <BlockerBox level={level} slot={0} x={x + 18} y={y} />
              <BlockerBox level={level} slot={1} x={x + 50} y={y} />
            </div>
          )
        }),
      )}
    </>
  )
}

export function Notes() {
  const { state, dispatch } = useTracker()
  const field = (name: 'seed' | 'flags', placeholder: string, y: number) => (
    <input
      aria-label={placeholder}
      placeholder={placeholder}
      value={state[name]}
      onChange={(e) => dispatch({ type: 'setText', field: name, value: e.target.value })}
      style={{
        ...at(452, y, 312, 20),
        background: '#000',
        color: '#ffa500',
        border: 0,
        borderBottom: '1px solid #333',
        fontSize: 13,
        padding: '0 2px',
        textAlign: 'right',
      }}
    />
  )
  return (
    <>
      <div style={{ ...at(458, 624), fontSize: 18, color: '#33ff33' }}>Notes</div>
      <textarea
        aria-label="Notes"
        value={state.notes}
        onChange={(e) => dispatch({ type: 'setText', field: 'notes', value: e.target.value })}
        placeholder="Hints, shop prices, anything worth remembering..."
        style={{
          ...at(452, 648, 314, 138),
          background: '#000',
          color: '#33ff33',
          border: 0,
          resize: 'none',
          fontSize: 15,
          lineHeight: '19px',
          padding: '2px 6px',
        }}
      />
      {field('seed', 'Seed', 794)}
      {field('flags', 'Flags', 816)}
    </>
  )
}

export function OptionsButton() {
  const { state, dispatch } = useTracker()
  const openPicker = usePicker()
  const fileRef = useRef<HTMLInputElement>(null)

  const items: PickerItem[] = [
    { id: 1, title: 'Save to file', node: <span style={{ color: '#ffa500', padding: '0 8px' }}>Save to file</span> },
    { id: 2, title: 'Load from file', node: <span style={{ color: '#ffa500', padding: '0 8px' }}>Load from file</span> },
    { id: 3, title: 'Reset everything', node: <span style={{ color: '#ff6b6b', padding: '0 8px' }}>Reset everything</span> },
  ]

  function onPick(id: number) {
    if (id === 1) {
      const url = URL.createObjectURL(new Blob([serialize(state)], { type: 'application/json' }))
      const a = document.createElement('a')
      a.href = url
      a.download = 'z-tracker-save.json'
      a.click()
      URL.revokeObjectURL(url)
    } else if (id === 2) {
      fileRef.current?.click()
    } else if (window.confirm('Reset the whole tracker?')) {
      dispatch({ type: 'reset' })
    }
  }

  return (
    <>
      <button
        type="button"
        className="orange-btn"
        style={at(8, 846, 92, 20)}
        onClick={(e) => openPicker(e, { title: 'Options', items, columns: 1, onPick })}
      >
        Options...
      </button>
      <input
        ref={fileRef}
        type="file"
        accept="application/json,.json"
        aria-label="Load save file"
        hidden
        onChange={async (e) => {
          const file = e.target.files?.[0]
          e.target.value = ''
          if (!file) return
          const loaded = parseSaved(await file.text())
          if (loaded) dispatch({ type: 'load', state: loaded })
          else window.alert('That file is not a valid tracker save.')
        }}
      />
    </>
  )
}
