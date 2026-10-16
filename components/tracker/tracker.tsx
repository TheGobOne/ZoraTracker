'use client'

import { useEffect, useState } from 'react'
import { TrackerProvider } from '@/lib/tracker-state'
import { DungeonTracker } from './dungeon'
import { Header } from './header'
import { ItemProgress, Legend, OverworldMap } from './overworld'
import { PickerProvider } from './picker'
import { Blockers, Notes, OptionsButton } from './side-panels'
import { Timeline } from './timeline'

const WIDTH = 768
const HEIGHT = 967

export function Tracker() {
  const [scale, setScale] = useState(1)
  const [coords, setCoords] = useState(false)

  useEffect(() => {
    const fit = () => setScale(Math.max(0.5, Math.min(window.innerWidth / WIDTH, Math.max(window.innerHeight / HEIGHT, 1))))
    fit()
    window.addEventListener('resize', fit)
    return () => window.removeEventListener('resize', fit)
  }, [])

  return (
    <TrackerProvider>
      <PickerProvider>
        <div style={{ width: WIDTH * scale, height: HEIGHT * scale, margin: '0 auto' }}>
          <div
            className="zt"
            style={{
              position: 'relative',
              width: WIDTH,
              height: HEIGHT,
              background: '#000',
              color: '#ffa500',
              overflow: 'hidden',
              transform: `scale(${scale})`,
              transformOrigin: '0 0',
              userSelect: 'none',
            }}
          >
            <Header coords={coords} onCoords={setCoords} />
            <OverworldMap coords={coords} />
            <Legend />
            <ItemProgress />
            <DungeonTracker />
            <Blockers />
            <Notes />
            <Timeline />
            <OptionsButton />
          </div>
        </div>
      </PickerProvider>
    </TrackerProvider>
  )
}
