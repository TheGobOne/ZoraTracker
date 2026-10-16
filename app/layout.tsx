import type { Metadata, Viewport } from 'next'
import { JetBrains_Mono, Press_Start_2P } from 'next/font/google'
import './globals.css'

const pixel = Press_Start_2P({
  weight: '400',
  subsets: ['latin'],
  variable: '--font-pixel-src',
})

const body = JetBrains_Mono({
  subsets: ['latin'],
  variable: '--font-body-src',
})

export const metadata: Metadata = {
  title: 'Z-Tracker for Zelda 1 Randomizer (Web)',
  description:
    'A browser-based tracker for the Legend of Zelda 1 Randomizer: dungeon items, overworld map, dungeon room tracking, blockers, notes and a run timeline.',
}

export const viewport: Viewport = {
  themeColor: '#0a0c08',
  colorScheme: 'dark',
  width: 'device-width',
  initialScale: 1,
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${pixel.variable} ${body.variable}`}>
      <body>{children}</body>
    </html>
  )
}
