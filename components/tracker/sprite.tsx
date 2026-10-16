import type { CSSProperties } from 'react'

interface Sheet {
  src: string
  w: number
  h: number
  cw: number
  ch: number
}

export const SHEETS = {
  items16: { src: '/sprites/zelda_items16x16.png', w: 208, h: 16, cw: 16, ch: 16 },
  icons7: { src: '/sprites/icons7x7.png', w: 224, h: 7, cw: 7, ch: 7 },
  icons10: { src: '/sprites/icons10x10.png', w: 100, h: 10, cw: 10, ch: 10 },
  link: { src: '/sprites/link_icons.png', w: 64, h: 16, cw: 16, ch: 16 },
} satisfies Record<string, Sheet>

/** A single frame of a sprite sheet, nearest-neighbour scaled. */
export function Sprite({
  sheet,
  index,
  scale = 3,
  style,
}: {
  sheet: keyof typeof SHEETS
  index: number
  scale?: number
  style?: CSSProperties
}) {
  const s = SHEETS[sheet]
  return (
    <div
      aria-hidden="true"
      className="px"
      style={{
        width: s.cw * scale,
        height: s.ch * scale,
        backgroundImage: `url(${s.src})`,
        backgroundSize: `${s.w * scale}px ${s.h * scale}px`,
        backgroundPosition: `${-index * s.cw * scale}px 0`,
        backgroundRepeat: 'no-repeat',
        ...style,
      }}
    />
  )
}

/** One 16x11 overworld screen, drawn at 3x (48x33) from the five-map strip. */
export function MapTile({ map, x, y }: { map: number; x: number; y: number }) {
  return (
    <div
      aria-hidden="true"
      className="px"
      style={{
        width: 48,
        height: 33,
        backgroundImage: 'url(/sprites/s_map_overworld_vanilla_strip8.png)',
        backgroundSize: '3840px 264px',
        backgroundPosition: `${-(map * 256 + x * 16) * 3}px ${-y * 11 * 3}px`,
      }}
    />
  )
}

/** Sprite index in icons7x7 for each item code. */
export const ICON7: Record<string, number> = {
  BK: 8,
  BO: 0,
  MB: 2,
  BW: 1,
  SA: 10,
  WA: 6,
  BR: 22,
  RR: 12,
  BC: 21,
  RC: 7,
  PB: 16,
  LD: 4,
  RF: 3,
  RE: 5,
  MK: 9,
  WS: 17,
  WD: 19,
  MS: 20,
  HC: 15,
  BB: 31,
  ME: 27,
  PT: 28,
  AR: 11,
  CN: 21,
  RG: 22,
  BM: 25,
  GA: 23,
  ZE: 24,
  LP: 28,
  '2P': 28,
}

/** Sprite index in zelda_items16x16 for dungeon-only items. */
export const ITEMS16: Record<string, number> = { KY: 3, MP: 5, CP: 6 }

export function ItemIcon({ code, dim = false }: { code: string; dim?: boolean }) {
  const opacity = dim ? 0.3 : 1
  if (code in ICON7) {
    return <Sprite sheet="icons7" index={ICON7[code]} style={{ opacity }} />
  }
  if (code in ITEMS16) {
    return <Sprite sheet="items16" index={ITEMS16[code]} scale={1} style={{ opacity }} />
  }
  return (
    <span style={{ opacity, color: '#fff', fontSize: 11, fontWeight: 700 }}>{code}</span>
  )
}
