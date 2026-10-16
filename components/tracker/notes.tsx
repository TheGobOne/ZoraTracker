'use client'

import { useTracker } from '@/lib/tracker-state'

export function Notes() {
  const { state, dispatch } = useTracker()

  return (
    <section className="panel flex flex-col gap-3 p-3" aria-labelledby="notes-title">
      <h2 id="notes-title" className="section-title">
        Notes
      </h2>
      <textarea
        value={state.notes}
        onChange={(e) => dispatch({ type: 'setText', field: 'notes', value: e.target.value })}
        placeholder="Hints, shop prices, anything worth remembering..."
        aria-label="Notes"
        rows={7}
        className="scroll-thin min-h-32 w-full resize-y border border-line bg-black p-2 text-[13px] text-good placeholder:text-muted"
      />
      <div className="grid gap-2 sm:grid-cols-2">
        <label className="flex flex-col gap-1 text-[11px] text-muted">
          Seed
          <input
            value={state.seed}
            onChange={(e) => dispatch({ type: 'setText', field: 'seed', value: e.target.value })}
            className="border border-line bg-black px-2 py-1 text-[12px] text-foreground"
            autoComplete="off"
            spellCheck={false}
          />
        </label>
        <label className="flex flex-col gap-1 text-[11px] text-muted">
          Flags
          <input
            value={state.flags}
            onChange={(e) => dispatch({ type: 'setText', field: 'flags', value: e.target.value })}
            className="border border-line bg-black px-2 py-1 text-[12px] text-foreground"
            autoComplete="off"
            spellCheck={false}
          />
        </label>
      </div>
    </section>
  )
}
