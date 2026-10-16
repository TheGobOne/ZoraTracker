'use client'

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useReducer,
  useState,
  type ReactNode,
} from 'react'
import {
  BOX_ITEMS,
  DARK_X,
  DUNGEON_SIZE,
  HEART_CONTAINER,
  OW_COUNT,
  TILES,
  type Quest,
} from './data'

export interface OwTile {
  marker: number // -1 when unmarked
  screen: 0 | 1 | 2 // for mixed quest: which version of the screen is shown
}

export interface TimelineEvent {
  t: number
  kind: 'triforce' | 'item' | 'dungeon' | 'sword' | 'road'
  label: string
}

export interface TimerState {
  baseMs: number
  startedAt: number | null
}

export interface TrackerState {
  version: 1
  quest: Quest
  triforces: boolean[]
  boxes: number[][] // [level 0..8][slot 0..2] item id or 0
  inventory: Record<string, number>
  bonusHearts: number
  tiles: OwTile[]
  rooms: number[][] // [level][64]
  hDoors: number[][] // [level][8 rows * 7 gaps]
  vDoors: number[][] // [level][7 gaps * 8 cols]
  blockers: number[][] // [level 0..7][slot 0..1]
  notes: string
  seed: string
  flags: string
  timer: TimerState
  events: TimelineEvent[]
  owHistory: { t: number; n: number }[]
  highlightRemaining: boolean
}

export const BOXES_PER_LEVEL = 3

function makeInitial(): TrackerState {
  const levels = Array.from({ length: 9 }, (_, i) => i)
  return {
    version: 1,
    quest: 'unknown',
    triforces: Array(8).fill(false),
    boxes: levels.map(() => Array(BOXES_PER_LEVEL).fill(0)),
    inventory: {},
    bonusHearts: 0,
    tiles: Array.from({ length: OW_COUNT }, () => ({ marker: -1, screen: 0 as const })),
    rooms: levels.map(() => Array(DUNGEON_SIZE * DUNGEON_SIZE).fill(0)),
    hDoors: levels.map(() => Array(DUNGEON_SIZE * (DUNGEON_SIZE - 1)).fill(0)),
    vDoors: levels.map(() => Array((DUNGEON_SIZE - 1) * DUNGEON_SIZE).fill(0)),
    blockers: Array.from({ length: 8 }, () => [0, 0]),
    notes: '',
    seed: '',
    flags: '',
    timer: { baseMs: 0, startedAt: null },
    events: [],
    owHistory: [],
    highlightRemaining: false,
  }
}

export function elapsedMs(timer: TimerState, now: number): number {
  return timer.baseMs + (timer.startedAt !== null ? now - timer.startedAt : 0)
}

export function formatTime(ms: number): string {
  const total = Math.max(0, Math.floor(ms / 1000))
  const h = Math.floor(total / 3600)
  const m = Math.floor((total % 3600) / 60)
  const s = total % 60
  return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
}

type TimedAction = { now: number }

export type Action =
  | ({ type: 'setQuest'; quest: Quest } & TimedAction)
  | ({ type: 'toggleTriforce'; level: number } & TimedAction)
  | ({ type: 'setBox'; level: number; slot: number; item: number } & TimedAction)
  | ({ type: 'setInventory'; key: string; value: number } & TimedAction)
  | ({ type: 'setBonusHearts'; value: number } & TimedAction)
  | ({ type: 'setTile'; index: number; marker: number } & TimedAction)
  | ({ type: 'setTileScreen'; index: number; screen: 0 | 1 | 2 } & TimedAction)
  | ({ type: 'setRoom'; level: number; index: number; value: number } & TimedAction)
  | ({ type: 'setDoor'; level: number; axis: 'h' | 'v'; index: number; value: number } & TimedAction)
  | ({ type: 'clearDungeon'; level: number } & TimedAction)
  | ({ type: 'setBlocker'; level: number; slot: number; value: number } & TimedAction)
  | ({ type: 'setText'; field: 'notes' | 'seed' | 'flags'; value: string } & TimedAction)
  | ({ type: 'toggleTimer' } & TimedAction)
  | ({ type: 'resetTimer' } & TimedAction)
  | ({ type: 'toggleHighlight' } & TimedAction)
  | ({ type: 'load'; state: TrackerState } & TimedAction)
  | ({ type: 'reset' } & TimedAction)

function replaceAt<T>(arr: T[], index: number, value: T): T[] {
  const next = arr.slice()
  next[index] = value
  return next
}

function markedCount(tiles: OwTile[]): number {
  let n = 0
  for (const t of tiles) if (t.marker >= 0) n++
  return n
}

function reducer(state: TrackerState, action: Action): TrackerState {
  const at = elapsedMs(state.timer, action.now)
  switch (action.type) {
    case 'setQuest':
      return { ...state, quest: action.quest }

    case 'toggleTriforce': {
      const had = state.triforces[action.level]
      const events = had
        ? state.events
        : [
            ...state.events,
            { t: at, kind: 'triforce' as const, label: String(action.level + 1) },
          ]
      return { ...state, triforces: replaceAt(state.triforces, action.level, !had), events }
    }

    case 'setBox': {
      const row = replaceAt(state.boxes[action.level], action.slot, action.item)
      const def = BOX_ITEMS.find((i) => i.id === action.item)
      const events =
        def && state.boxes[action.level][action.slot] !== action.item
          ? [...state.events, { t: at, kind: 'item' as const, label: def.code }]
          : state.events
      return { ...state, boxes: replaceAt(state.boxes, action.level, row), events }
    }

    case 'setInventory': {
      const inventory = { ...state.inventory, [action.key]: action.value }
      return { ...state, inventory }
    }

    case 'setBonusHearts':
      return { ...state, bonusHearts: Math.max(0, Math.min(13, action.value)) }

    case 'setTile': {
      const prev = state.tiles[action.index]
      const tiles = replaceAt(state.tiles, action.index, { ...prev, marker: action.marker })
      let events = state.events
      const def = TILES.find((t) => t.id === action.marker)
      if (def && prev.marker !== action.marker) {
        if (def.group === 'dungeon')
          events = [...events, { t: at, kind: 'dungeon', label: def.label }]
        else if (def.group === 'road') events = [...events, { t: at, kind: 'road', label: def.label }]
        else if (def.group === 'cave') events = [...events, { t: at, kind: 'sword', label: def.label }]
      }
      return {
        ...state,
        tiles,
        events,
        owHistory: [...state.owHistory, { t: at, n: markedCount(tiles) }],
      }
    }

    case 'setTileScreen': {
      const prev = state.tiles[action.index]
      return {
        ...state,
        tiles: replaceAt(state.tiles, action.index, { ...prev, screen: action.screen }),
      }
    }

    case 'setRoom': {
      const row = replaceAt(state.rooms[action.level], action.index, action.value)
      return { ...state, rooms: replaceAt(state.rooms, action.level, row) }
    }

    case 'setDoor': {
      const key = action.axis === 'h' ? 'hDoors' : 'vDoors'
      const row = replaceAt(state[key][action.level], action.index, action.value)
      return { ...state, [key]: replaceAt(state[key], action.level, row) }
    }

    case 'clearDungeon': {
      const fresh = makeInitial()
      return {
        ...state,
        rooms: replaceAt(state.rooms, action.level, fresh.rooms[0]),
        hDoors: replaceAt(state.hDoors, action.level, fresh.hDoors[0]),
        vDoors: replaceAt(state.vDoors, action.level, fresh.vDoors[0]),
      }
    }

    case 'setBlocker': {
      const row = replaceAt(state.blockers[action.level], action.slot, action.value)
      return { ...state, blockers: replaceAt(state.blockers, action.level, row) }
    }

    case 'setText':
      return { ...state, [action.field]: action.value }

    case 'toggleTimer': {
      const running = state.timer.startedAt !== null
      return {
        ...state,
        timer: running
          ? { baseMs: at, startedAt: null }
          : { baseMs: state.timer.baseMs, startedAt: action.now },
      }
    }

    case 'resetTimer':
      return { ...state, timer: { baseMs: 0, startedAt: null } }

    case 'toggleHighlight':
      return { ...state, highlightRemaining: !state.highlightRemaining }

    case 'load':
      return action.state

    case 'reset':
      return makeInitial()
  }
}

/* Derived values ------------------------------------------------------- */

export function maxHearts(state: TrackerState): number {
  let boxHearts = 0
  for (const level of state.boxes)
    for (const item of level) if (item === HEART_CONTAINER) boxHearts++
  return Math.min(16, 3 + boxHearts + state.bonusHearts)
}

export function spotsMarked(state: TrackerState): number {
  return markedCount(state.tiles)
}

/* Provider ------------------------------------------------------------- */

type DistributiveOmit<T, K extends keyof never> = T extends unknown ? Omit<T, K> : never
export type TrackerAction = DistributiveOmit<Action, 'now'>

interface TrackerContextValue {
  state: TrackerState
  dispatch: (action: TrackerAction) => void
}

const TrackerContext = createContext<TrackerContextValue | null>(null)

export function TrackerProvider({ children }: { children: ReactNode }) {
  const [state, rawDispatch] = useReducer(reducer, undefined, makeInitial)

  const dispatch = useCallback(
    (action: TrackerAction) => rawDispatch({ ...action, now: Date.now() } as Action),
    [],
  )

  const value = useMemo(() => ({ state, dispatch }), [state, dispatch])
  return <TrackerContext.Provider value={value}>{children}</TrackerContext.Provider>
}

export function useTracker(): TrackerContextValue {
  const ctx = useContext(TrackerContext)
  if (!ctx) throw new Error('useTracker must be used inside TrackerProvider')
  return ctx
}

/** Current elapsed run time, re-rendering while the timer is running. */
export function useElapsed(): number {
  const { state } = useTracker()
  const running = state.timer.startedAt !== null
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    setNow(Date.now())
    if (!running) return
    const id = window.setInterval(() => setNow(Date.now()), 250)
    return () => window.clearInterval(id)
  }, [running])

  return elapsedMs(state.timer, now)
}

/* Save / load ---------------------------------------------------------- */

export function serialize(state: TrackerState): string {
  return JSON.stringify({ ...state, timer: { baseMs: elapsedMs(state.timer, Date.now()), startedAt: null } }, null, 2)
}

export function parseSaved(text: string): TrackerState | null {
  try {
    const data = JSON.parse(text) as Partial<TrackerState>
    if (!data || data.version !== 1 || !Array.isArray(data.tiles) || data.tiles.length !== OW_COUNT) {
      return null
    }
    const base = makeInitial()
    return { ...base, ...data, timer: { baseMs: data.timer?.baseMs ?? 0, startedAt: null } } as TrackerState
  } catch {
    return null
  }
}

export { DARK_X }
