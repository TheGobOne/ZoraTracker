import type { CSSProperties } from 'react'

interface ChipProps {
  code: string
  color: string
  dim?: boolean
  size?: number
  className?: string
}

/** A small pixel-styled badge standing in for an item sprite. */
export function Chip({ code, color, dim = false, size = 28, className = '' }: ChipProps) {
  const style: CSSProperties = {
    width: size,
    height: size,
    color: dim ? '#4a4f3f' : color,
    borderColor: dim ? '#2b3220' : color,
    fontSize: size <= 20 ? 8 : 9,
  }
  return (
    <span
      className={`inline-flex shrink-0 items-center justify-center border bg-black font-pixel leading-none ${className}`}
      style={style}
      aria-hidden="true"
    >
      {code}
    </span>
  )
}

interface SpriteProps {
  sheet: 'items' | 'bosses'
  index: number
  size?: number
  dim?: boolean
}

/** Draws one 16x16 cell from the original zelda_items16x16 / zelda_bosses16x16 sheets. */
export function Sprite({ sheet, index, size = 32, dim = false }: SpriteProps) {
  const file = sheet === 'items' ? 'zelda_items16x16.png' : 'zelda_bosses16x16.png'
  const count = sheet === 'items' ? 13 : 32
  return (
    <span
      aria-hidden="true"
      className="pixelated inline-block shrink-0"
      style={{
        width: size,
        height: size,
        backgroundImage: `url(/sprites/${file})`,
        backgroundSize: `${count * 100}% 100%`,
        backgroundPosition: `${(index / (count - 1)) * 100}% 0`,
        opacity: dim ? 0.3 : 1,
      }}
    />
  )
}
