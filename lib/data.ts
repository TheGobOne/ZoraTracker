export const OW_W = 16
export const OW_H = 8
export const OW_COUNT = OW_W * OW_H

export type Quest = 'first' | 'second' | 'mixed' | 'unknown'

export const QUEST_LABEL: Record<Quest, string> = {
  first: 'First Quest',
  second: 'Second Quest',
  mixed: 'Mixed Quest',
  unknown: 'Unknown',
}

// Number of overworld screens that can still hold something, per quest type.
export const MAX_SPOTS: Record<Quest, number> = {
  first: 73,
  second: 80,
  mixed: 93,
  unknown: 128,
}

const FIRST_EMPTY = [
  'X.X...X.XX......',
  '.X...X.XXX.X....',
  'X........XXX..X.',
  'XXX..XX.XXXX..XX',
  'XX.X........X..X',
  'X.XXXX.XXXX.XX.X',
  'XX...X...X..X.X.',
  '..XX......X...XX',
]

const SECOND_EMPTY = [
  '.....X..X..X....',
  '.......X........',
  '.X.....X..X.X.X.',
  '.XX..XX.XX.X..XX',
  'XXXX...X....X..X',
  'X.X.XX.X.XX.XX.X',
  '.XX..X.X.X.X.X..',
  '.X.X......XX..XX',
]

const MIXED_SOMETIMES = [
  'X.X..XX..X......',
  '.X...X..XX.X....',
  'XX.....X.X.XX...',
  'X.........X.....',
  '.......X........',
  '...X....X.......',
  'X.X....X...XXXX.',
  '.XX........X....',
]

export function isAlwaysEmpty(quest: Quest, x: number, y: number): boolean {
  switch (quest) {
    case 'first':
      return FIRST_EMPTY[y][x] === 'X'
    case 'second':
      return SECOND_EMPTY[y][x] === 'X'
    case 'mixed':
      return FIRST_EMPTY[y][x] === 'X' && SECOND_EMPTY[y][x] === 'X'
    default:
      return false
  }
}

export function isSometimesEmpty(quest: Quest, x: number, y: number): boolean {
  return quest === 'mixed' && MIXED_SOMETIMES[y][x] === 'X'
}

/* Overworld markers --------------------------------------------------- */

export type TileGroup = 'dungeon' | 'road' | 'cave' | 'shop' | 'secret' | 'other'

export interface TileDef {
  id: number
  label: string
  name: string
  group: TileGroup
  max: number
}

const T = (id: number, label: string, name: string, group: TileGroup, max = 99): TileDef => ({
  id,
  label,
  name,
  group,
  max,
})

export const TILES: TileDef[] = [
  T(0, '1', 'Level 1', 'dungeon', 1),
  T(1, '2', 'Level 2', 'dungeon', 1),
  T(2, '3', 'Level 3', 'dungeon', 1),
  T(3, '4', 'Level 4', 'dungeon', 1),
  T(4, '5', 'Level 5', 'dungeon', 1),
  T(5, '6', 'Level 6', 'dungeon', 1),
  T(6, '7', 'Level 7', 'dungeon', 1),
  T(7, '8', 'Level 8', 'dungeon', 1),
  T(8, '9', 'Level 9', 'dungeon', 1),
  T(9, 'A', 'Any Road 1', 'road', 1),
  T(10, 'B', 'Any Road 2', 'road', 1),
  T(11, 'C', 'Any Road 3', 'road', 1),
  T(12, 'D', 'Any Road 4', 'road', 1),
  T(13, 'MS', 'Magical Sword cave', 'cave', 1),
  T(14, 'WS', 'White Sword cave', 'cave', 1),
  T(15, 'WD', 'Wooden Sword cave', 'cave', 1),
  T(16, 'AR', 'Arrow shop', 'shop'),
  T(17, 'BM', 'Bomb shop', 'shop'),
  T(18, 'BK', 'Book shop', 'shop'),
  T(19, 'CN', 'Candle shop', 'shop'),
  T(20, 'RG', 'Ring shop', 'shop'),
  T(21, 'ME', 'Meat shop', 'shop'),
  T(22, 'KY', 'Key shop', 'shop'),
  T(23, 'SH', 'Shield shop', 'shop'),
  T(24, '?$', 'Unknown secret', 'secret'),
  T(25, 'L$', 'Large secret', 'secret'),
  T(26, 'M$', 'Medium secret', 'secret'),
  T(27, 'S$', 'Small secret', 'secret'),
  T(28, 'DR', 'Door repair charge', 'secret'),
  T(29, 'MG', 'Money making game', 'secret'),
  T(30, 'LT', 'The Letter', 'other', 1),
  T(31, 'AM', 'Armos item', 'other', 1),
  T(32, 'HT', 'Hint shop', 'other'),
  T(33, 'TA', 'Take any', 'other'),
  T(34, 'PO', 'Potion shop', 'other'),
  T(35, '\u00d7', 'Nothing here', 'other'),
]

export const DARK_X = 35

export const GROUP_LABEL: Record<TileGroup, string> = {
  dungeon: 'Dungeons',
  road: 'Any Roads (warps)',
  cave: 'Sword caves',
  shop: 'Shops',
  secret: 'Secrets',
  other: 'Other',
}

/* Items ---------------------------------------------------------------- */

export interface ItemDef {
  id: number
  code: string
  name: string
  color: string
}

// Items that can be found in a dungeon item box.
export const BOX_ITEMS: ItemDef[] = [
  { id: 1, code: 'BK', name: 'Book of Magic', color: '#5b8def' },
  { id: 2, code: 'BO', name: 'Boomerang', color: '#c58a4a' },
  { id: 3, code: 'MB', name: 'Magical Boomerang', color: '#4a7cd4' },
  { id: 4, code: 'BW', name: 'Bow', color: '#c58a4a' },
  { id: 5, code: 'SA', name: 'Silver Arrows', color: '#cfd6e3' },
  { id: 6, code: 'WA', name: 'Magical Rod (Wand)', color: '#5b8def' },
  { id: 7, code: 'BR', name: 'Blue Ring', color: '#5b8def' },
  { id: 8, code: 'RR', name: 'Red Ring', color: '#e5484d' },
  { id: 9, code: 'BC', name: 'Blue Candle', color: '#5b8def' },
  { id: 10, code: 'RC', name: 'Red Candle', color: '#e5484d' },
  { id: 11, code: 'PB', name: 'Power Bracelet', color: '#e5484d' },
  { id: 12, code: 'LD', name: 'Ladder', color: '#c58a4a' },
  { id: 13, code: 'RF', name: 'Raft', color: '#c58a4a' },
  { id: 14, code: 'RE', name: 'Recorder', color: '#e5484d' },
  { id: 15, code: 'MK', name: 'Magical Key', color: '#f2c94c' },
  { id: 16, code: 'WS', name: 'White Sword', color: '#cfd6e3' },
  { id: 17, code: 'MS', name: 'Magical Sword', color: '#5b8def' },
  { id: 18, code: 'HC', name: 'Heart Container', color: '#e5484d' },
  { id: 19, code: 'BB', name: 'Bomb upgrade', color: '#5b8def' },
  { id: 20, code: 'ME', name: 'Meat (bait)', color: '#c58a4a' },
  { id: 21, code: 'PT', name: 'Potion', color: '#4ade80' },
  { id: 22, code: 'KY', name: 'Key', color: '#f2c94c' },
  { id: 23, code: 'MP', name: 'Map', color: '#4ade80' },
  { id: 24, code: 'CP', name: 'Compass', color: '#4ade80' },
]

export const HEART_CONTAINER = 18

export interface InventoryDef {
  key: string
  name: string
  // Index 0 is "don't have it"; each following entry is an upgrade level.
  levels: { code: string; name: string; color: string }[]
}

export const INVENTORY: InventoryDef[] = [
  {
    key: 'sword',
    name: 'Sword',
    levels: [
      { code: '--', name: 'No sword', color: '#555' },
      { code: 'WD', name: 'Wooden Sword', color: '#c58a4a' },
      { code: 'WS', name: 'White Sword', color: '#cfd6e3' },
      { code: 'MS', name: 'Magical Sword', color: '#5b8def' },
    ],
  },
  {
    key: 'boomerang',
    name: 'Boomerang',
    levels: [
      { code: 'BO', name: 'No boomerang', color: '#555' },
      { code: 'BO', name: 'Boomerang', color: '#c58a4a' },
      { code: 'MB', name: 'Magical Boomerang', color: '#5b8def' },
    ],
  },
  {
    key: 'bow',
    name: 'Bow',
    levels: [
      { code: 'BW', name: 'No bow', color: '#555' },
      { code: 'BW', name: 'Bow', color: '#c58a4a' },
    ],
  },
  {
    key: 'arrows',
    name: 'Arrows',
    levels: [
      { code: 'AR', name: 'No arrows', color: '#555' },
      { code: 'AR', name: 'Wooden Arrows', color: '#c58a4a' },
      { code: 'SA', name: 'Silver Arrows', color: '#cfd6e3' },
    ],
  },
  {
    key: 'candle',
    name: 'Candle',
    levels: [
      { code: 'CN', name: 'No candle', color: '#555' },
      { code: 'BC', name: 'Blue Candle', color: '#5b8def' },
      { code: 'RC', name: 'Red Candle', color: '#e5484d' },
    ],
  },
  {
    key: 'ring',
    name: 'Ring',
    levels: [
      { code: 'RG', name: 'No ring', color: '#555' },
      { code: 'BR', name: 'Blue Ring', color: '#5b8def' },
      { code: 'RR', name: 'Red Ring', color: '#e5484d' },
    ],
  },
  {
    key: 'potion',
    name: 'Potion',
    levels: [
      { code: 'PT', name: 'No potion', color: '#555' },
      { code: 'LP', name: 'Life Potion', color: '#4ade80' },
      { code: '2P', name: 'Second Potion', color: '#e5484d' },
    ],
  },
  {
    key: 'wand',
    name: 'Magical Rod',
    levels: [
      { code: 'WA', name: 'No wand', color: '#555' },
      { code: 'WA', name: 'Magical Rod (Wand)', color: '#5b8def' },
    ],
  },
  {
    key: 'book',
    name: 'Book of Magic',
    levels: [
      { code: 'BK', name: 'No book', color: '#555' },
      { code: 'BK', name: 'Book of Magic', color: '#5b8def' },
    ],
  },
  {
    key: 'ladder',
    name: 'Ladder',
    levels: [
      { code: 'LD', name: 'No ladder', color: '#555' },
      { code: 'LD', name: 'Ladder', color: '#c58a4a' },
    ],
  },
  {
    key: 'raft',
    name: 'Raft',
    levels: [
      { code: 'RF', name: 'No raft', color: '#555' },
      { code: 'RF', name: 'Raft', color: '#c58a4a' },
    ],
  },
  {
    key: 'recorder',
    name: 'Recorder',
    levels: [
      { code: 'RE', name: 'No recorder', color: '#555' },
      { code: 'RE', name: 'Recorder', color: '#e5484d' },
    ],
  },
  {
    key: 'bracelet',
    name: 'Power Bracelet',
    levels: [
      { code: 'PB', name: 'No bracelet', color: '#555' },
      { code: 'PB', name: 'Power Bracelet', color: '#e5484d' },
    ],
  },
  {
    key: 'key',
    name: 'Magical Key',
    levels: [
      { code: 'MK', name: 'No magical key', color: '#555' },
      { code: 'MK', name: 'Magical Key', color: '#f2c94c' },
    ],
  },
  {
    key: 'bait',
    name: 'Bait',
    levels: [
      { code: 'ME', name: 'No bait', color: '#555' },
      { code: 'ME', name: 'Meat (bait)', color: '#c58a4a' },
    ],
  },
  {
    key: 'letter',
    name: 'Letter',
    levels: [
      { code: 'LT', name: 'No letter', color: '#555' },
      { code: 'LT', name: 'Letter to the Old Woman', color: '#cfd6e3' },
    ],
  },
]

/* Blockers ------------------------------------------------------------- */

export const BLOCKERS: { code: string; name: string; color: string }[] = [
  { code: '', name: 'Empty', color: '#555' },
  { code: 'KY', name: 'Key needed', color: '#f2c94c' },
  { code: 'LD', name: 'Ladder needed', color: '#c58a4a' },
  { code: 'RE', name: 'Recorder needed', color: '#e5484d' },
  { code: 'ME', name: 'Bait needed', color: '#c58a4a' },
  { code: 'BW', name: 'Bow and arrows needed', color: '#c58a4a' },
  { code: 'BM', name: 'Bombs needed', color: '#5b8def' },
  { code: 'CN', name: 'Candle needed', color: '#e5484d' },
  { code: 'WA', name: 'Wand needed', color: '#5b8def' },
  { code: 'BK', name: 'Book needed', color: '#5b8def' },
  { code: 'RG', name: 'Ring needed', color: '#4ade80' },
]

/* Dungeon rooms -------------------------------------------------------- */

export interface RoomDef {
  id: number
  code: string
  name: string
  color: string
}

export const ROOMS: RoomDef[] = [
  { id: 0, code: '', name: 'Unexplored', color: 'transparent' },
  { id: 1, code: '', name: 'Visited room', color: '#6b7355' },
  { id: 2, code: '\u25b2', name: 'Entrance', color: '#4ade80' },
  { id: 3, code: 'S', name: 'Staircase', color: '#f2c94c' },
  { id: 4, code: 'T', name: 'Transport staircase', color: '#5b8def' },
  { id: 5, code: 'I', name: 'Item room', color: '#ef8300' },
  { id: 6, code: 'F', name: 'Triforce', color: '#f2c94c' },
  { id: 7, code: 'B', name: 'Boss', color: '#e5484d' },
  { id: 8, code: 'M', name: 'Map', color: '#4ade80' },
  { id: 9, code: 'C', name: 'Compass', color: '#4ade80' },
  { id: 10, code: 'K', name: 'Key', color: '#f2c94c' },
  { id: 11, code: 'H', name: 'Hint / old man', color: '#cfd6e3' },
  { id: 12, code: 'W', name: 'Wallmaster', color: '#c58a4a' },
  { id: 13, code: 'N', name: 'Not a room', color: '#222' },
]

export const DOORS: { id: number; name: string; color: string }[] = [
  { id: 0, name: 'Unknown', color: 'transparent' },
  { id: 1, name: 'Open door', color: '#6b7355' },
  { id: 2, name: 'Locked door', color: '#f2c94c' },
  { id: 3, name: 'Solid wall', color: '#e5484d' },
  { id: 4, name: 'Shutter / bombable', color: '#5b8def' },
]

export const DUNGEON_SIZE = 8
