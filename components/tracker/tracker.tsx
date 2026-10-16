'use client'

import { TrackerProvider } from '@/lib/tracker-state'
import { Blockers } from './blockers'
import { DungeonItems } from './dungeon-items'
import { DungeonTracker } from './dungeon-tracker'
import { HudBar } from './hud-bar'
import { Inventory } from './inventory'
import { Notes } from './notes'
import { OverworldMap } from './overworld-map'
import { Timeline } from './timeline'

export function Tracker() {
  return (
    <TrackerProvider>
      <div className="mx-auto flex w-full max-w-[1500px] flex-col gap-3 p-3">
        <HudBar />
        <div className="grid grid-cols-1 gap-3 xl:grid-cols-[minmax(0,1fr)_minmax(0,620px)]">
          <div className="flex min-w-0 flex-col gap-3">
            <DungeonItems />
            <Inventory />
            <OverworldMap />
          </div>
          <div className="flex min-w-0 flex-col gap-3">
            <DungeonTracker />
            <Blockers />
            <Notes />
          </div>
        </div>
        <Timeline />
      </div>
    </TrackerProvider>
  )
}
