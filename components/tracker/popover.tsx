'use client'

import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react'

interface PopoverProps {
  x: number
  y: number
  label: string
  onClose: () => void
  children: ReactNode
}

/** A floating panel pinned near (x, y) in viewport coordinates, clamped to the window. */
export function Popover({ x, y, label, onClose, children }: PopoverProps) {
  const ref = useRef<HTMLDivElement>(null)
  const [pos, setPos] = useState({ left: x, top: y })

  useLayoutEffect(() => {
    const el = ref.current
    if (!el) return
    const { width, height } = el.getBoundingClientRect()
    const left = Math.max(8, Math.min(x, window.innerWidth - width - 8))
    const top = y + height > window.innerHeight - 8 ? Math.max(8, y - height - 40) : y
    setPos({ left, top })
  }, [x, y])

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

  return (
    <div
      ref={ref}
      role="dialog"
      aria-label={label}
      className="fixed z-50 border border-accent bg-panel p-2 shadow-[0_8px_24px_rgba(0,0,0,0.7)]"
      style={{ left: pos.left, top: pos.top }}
    >
      {children}
    </div>
  )
}
