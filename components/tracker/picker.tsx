'use client'

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'

export interface PickerItem {
  id: number
  title: string
  node: ReactNode
  group?: string
}

interface PickerOptions {
  title: string
  items: PickerItem[]
  columns: number
  onPick: (id: number) => void
}

type OpenPicker = (at: { clientX: number; clientY: number }, options: PickerOptions) => void

const PickerContext = createContext<OpenPicker | null>(null)

export function usePicker(): OpenPicker {
  const ctx = useContext(PickerContext)
  if (!ctx) throw new Error('usePicker must be used inside PickerProvider')
  return ctx
}

interface OpenState extends PickerOptions {
  x: number
  y: number
}

export function PickerProvider({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState<OpenState | null>(null)
  const close = useCallback(() => setOpen(null), [])

  const openPicker = useCallback<OpenPicker>((at, options) => {
    setOpen({ ...options, x: at.clientX, y: at.clientY })
  }, [])

  const value = useMemo(() => openPicker, [openPicker])

  return (
    <PickerContext.Provider value={value}>
      {children}
      {open && <PickerPanel state={open} onClose={close} />}
    </PickerContext.Provider>
  )
}

function PickerPanel({ state, onClose }: { state: OpenState; onClose: () => void }) {
  const ref = useRef<HTMLDivElement>(null)
  const [pos, setPos] = useState({ left: state.x, top: state.y })

  useLayoutEffect(() => {
    const el = ref.current
    if (!el) return
    const { width, height } = el.getBoundingClientRect()
    setPos({
      left: Math.max(4, Math.min(state.x, window.innerWidth - width - 4)),
      top: Math.max(4, Math.min(state.y, window.innerHeight - height - 4)),
    })
  }, [state.x, state.y])

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    function onDown(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose()
    }
    window.addEventListener('keydown', onKey)
    window.addEventListener('mousedown', onDown)
    return () => {
      window.removeEventListener('keydown', onKey)
      window.removeEventListener('mousedown', onDown)
    }
  }, [onClose])

  // Group items while keeping their original order.
  const sections: { group?: string; items: PickerItem[] }[] = []
  for (const item of state.items) {
    const last = sections[sections.length - 1]
    if (last && last.group === item.group) last.items.push(item)
    else sections.push({ group: item.group, items: [item] })
  }

  return (
    <div
      ref={ref}
      role="dialog"
      aria-label={state.title}
      className="zt"
      style={{
        position: 'fixed',
        zIndex: 50,
        left: pos.left,
        top: pos.top,
        background: '#000',
        border: '2px solid #ffa500',
        padding: 6,
        maxHeight: 'calc(100vh - 8px)',
        overflowY: 'auto',
      }}
    >
      {sections.map((section, i) => (
        <div key={i} style={{ marginBottom: 4 }}>
          {section.group && (
            <div style={{ color: '#ffa500', fontSize: 11, margin: '2px 0 3px' }}>{section.group}</div>
          )}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: `repeat(${state.columns}, max-content)`,
              gap: 3,
            }}
          >
            {section.items.map((item) => (
              <button
                key={item.id}
                type="button"
                title={item.title}
                aria-label={item.title}
                onClick={() => {
                  state.onPick(item.id)
                  onClose()
                }}
                style={{
                  minWidth: 32,
                  minHeight: 32,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  background: '#0b0b12',
                  border: '1px solid #555',
                  padding: 2,
                }}
              >
                {item.node}
              </button>
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}
