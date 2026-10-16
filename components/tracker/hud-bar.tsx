'use client'

import { useRef, useState } from 'react'
import Image from 'next/image'
import { QUEST_LABEL, MAX_SPOTS, type Quest } from '@/lib/data'
import {
  formatTime,
  maxHearts,
  parseSaved,
  serialize,
  spotsMarked,
  useElapsed,
  useTracker,
} from '@/lib/tracker-state'

const QUESTS: Quest[] = ['unknown', 'first', 'second', 'mixed']

export function HudBar() {
  const { state, dispatch } = useTracker()
  const elapsed = useElapsed()
  const running = state.timer.startedAt !== null
  const fileRef = useRef<HTMLInputElement>(null)
  const [message, setMessage] = useState<string | null>(null)

  const remaining = MAX_SPOTS[state.quest] - spotsMarked(state)
  const triforces = state.triforces.filter(Boolean).length

  function save() {
    const blob = new Blob([serialize(state)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `z-tracker-${new Date().toISOString().slice(0, 10)}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

  async function load(file: File | undefined) {
    if (!file) return
    const parsed = parseSaved(await file.text())
    if (!parsed) {
      setMessage('That file is not a valid Z-Tracker save.')
      return
    }
    setMessage(null)
    dispatch({ type: 'load', state: parsed })
  }

  function reset() {
    if (window.confirm('Reset everything, including the timer? This cannot be undone.')) {
      dispatch({ type: 'reset' })
    }
  }

  return (
    <header className="panel flex flex-wrap items-center gap-x-6 gap-y-3 px-4 py-3">
      <div className="flex items-center gap-3">
        <Image
          src="/sprites/ZTlogo64x64.png"
          alt=""
          width={40}
          height={40}
          className="pixelated"
          priority
        />
        <div>
          <h1 className="font-pixel text-[11px] leading-relaxed text-accent">Z-TRACKER</h1>
          <p className="text-[11px] text-muted">Zelda 1 Randomizer · web</p>
        </div>
      </div>

      <div className="flex items-center gap-3">
        <div
          className="font-pixel text-2xl tabular-nums text-good"
          role="timer"
          aria-label="Elapsed run time"
        >
          {formatTime(elapsed)}
        </div>
        <button
          type="button"
          className="btn"
          onClick={() => dispatch({ type: 'toggleTimer' })}
          aria-pressed={running}
        >
          {running ? 'Pause' : elapsed > 0 ? 'Resume' : 'Start'}
        </button>
        <button
          type="button"
          className="btn"
          onClick={() => dispatch({ type: 'resetTimer' })}
          disabled={elapsed === 0 && !running}
        >
          Reset timer
        </button>
      </div>

      <div className="flex items-center gap-4 text-[12px]">
        <Stat label="Triforces" value={`${triforces}/8`} />
        <Stat label="Max hearts" value={String(maxHearts(state))} />
        <Stat
          label="OW spots left"
          value={String(Math.max(0, remaining))}
          title="Overworld screens not yet marked"
        />
      </div>

      <label className="flex items-center gap-2 text-[12px]">
        <span className="text-muted">Quest</span>
        <select
          className="border border-line bg-panel-2 px-2 py-1 text-foreground"
          value={state.quest}
          onChange={(e) => dispatch({ type: 'setQuest', quest: e.target.value as Quest })}
        >
          {QUESTS.map((q) => (
            <option key={q} value={q}>
              {QUEST_LABEL[q]}
            </option>
          ))}
        </select>
      </label>

      <div className="ml-auto flex flex-wrap items-center gap-2">
        <button type="button" className="btn" onClick={save}>
          Save
        </button>
        <button type="button" className="btn" onClick={() => fileRef.current?.click()}>
          Load
        </button>
        <input
          ref={fileRef}
          type="file"
          accept="application/json,.json"
          className="sr-only"
          aria-label="Load a saved tracker file"
          onChange={(e) => {
            void load(e.target.files?.[0])
            e.target.value = ''
          }}
        />
        <button type="button" className="btn hover:!border-bad hover:!text-bad" onClick={reset}>
          Reset all
        </button>
      </div>

      {message && (
        <p className="w-full text-[12px] text-bad" role="alert">
          {message}
        </p>
      )}
    </header>
  )
}

function Stat({ label, value, title }: { label: string; value: string; title?: string }) {
  return (
    <div className="flex flex-col" title={title}>
      <span className="text-[10px] uppercase tracking-wide text-muted">{label}</span>
      <span className="font-pixel text-[12px] text-gold">{value}</span>
    </div>
  )
}
