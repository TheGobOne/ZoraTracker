module FlagPaste

// Reads the flag strings from the ZORA site (the Z1R string and the ZORA string) and reports the tracker's startup settings they imply.
// The formats are from the ZORA repo (zora/flags/codec.py, fields.py and zora_flags.py): both are plain base-63 numbers in mixed radix.

open System.Numerics

let private alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!"

// a three-state flag: off, on, or "random" (decided per seed, so it can't be known from the string)
type Tri = Off | On | Random
let private tri (n:int) = match n with | 0 -> Off | 1 -> On | _ -> Random

// the Z1R string: the 27 option fields (C01..C27) by radix ("slots"), then 91 three-state fields (B01..B91); C01 is the least significant
let private z1rOptionSlots = [| 8;10;10;7;8;8;8;15;8;8;20;20;20;7;7;20;26;13;7;6;6;8;8;12;12;20;20 |]

// the ZORA string: "<version>.<payload>" (empty = every flag at its default); each field is (name, radix, version); first field least significant
let private zoraFields : (string * int * int) list =
    [ yield ("randomize_magical_sword", 2, 1)
      yield ("randomize_letter", 2, 1)
      yield ("magical_sword_hearts_highest", 6, 1)
      yield ("progressive_items", 2, 2)
      yield ("shop_items_in_pool", 2, 2)
      for n in [ "shuffle_blue_potion"; "add_l4_sword"; "extra_raft_blocks"; "extra_power_bracelet_blocks"; "speed_up_dungeon_transitions"
                 "speed_up_heart_fill"; "recorder_kills_pols_voice"; "four_potion_inventory"; "auto_show_letter"; "like_like_eats_rupees"
                 "magical_boomerang_damage"; "randomize_lost_hills"; "randomize_dead_woods" ] do
          yield (n, 3, 3)
      yield ("l4_sword_in_level_2", 2, 4)
      yield ("level_9_entrance_sword", 2, 4)
      yield ("oops_all_enemies", 4, 5)
      yield ("oops_all_bosses", 2, 5)
      yield ("allow_triforces_in_stairways", 3, 6)
      yield ("shuffle_wood_sword", 3, 6)
      yield ("pegasus_boots", 3, 7)
      yield ("expanded_dungeon_locations", 3, 8)
      yield ("highlight_no_drop_rooms", 3, 9)
      yield ("do_not_segment_dungeons", 3, 10)
      yield ("disable_custom_music", 2, 11)
      yield ("gatekeeper_bosses", 4, 12)
      yield ("full_health", 3, 13)
      yield ("harder_bosses", 4, 14)
      yield ("harder_ganon", 4, 14)
      yield ("offense_pacing", 4, 15)
      yield ("defense_pacing", 4, 15)
      yield ("recorder_warping", 2, 15)
      yield ("swap_swords_and_rings", 3, 16)
      yield ("swordlish", 3, 16)
      yield ("swordlish_lowest", 8, 16)
      yield ("swordlish_highest", 8, 16)
      yield ("item_shuffle_no_vanilla", 2, 17)
      yield ("compass_chime", 3, 18) ]
let private zoraCurrentVersion = 18

let private parseBase63 (s:string) : BigInteger option =
    let mutable n = BigInteger.Zero
    let mutable ok = s.Length > 0
    for c in s do
        let i = alphabet.IndexOf(c)
        if i < 0 then ok <- false
        else n <- n * BigInteger(63) + BigInteger(i)
    if ok then Some n else None

// what the Z1R string says about the settings the tracker has (None = not known from the string)
type Z1RPart = { Quest : int option          // 0 = 1Q, 1 = 2Q, 2 = Mixed 1Q, 3 = Mixed 2Q
                 ItemShuffle : int option    // 0 = Off, 1 = Intra, 2 = Full
                 Mirror : Tri
                 HideNumbers : Tri
                 Notes : string list }

let decodeZ1R (s:string) : Result<Z1RPart, string> =
    match parseBase63 (s.Trim()) with
    | None -> Error "That doesn't look like a Z1R flag string (it should use only letters, digits and !)."
    | Some number ->
        let mutable n = number
        let options = Array.zeroCreate<int> z1rOptionSlots.Length
        for i = 0 to z1rOptionSlots.Length - 1 do
            let slots = BigInteger(z1rOptionSlots.[i])
            options.[i] <- int (BigInteger.Remainder(n, slots))
            n <- BigInteger.Divide(n, slots)
        let toggles = Array.zeroCreate<int> 91
        for i = 0 to 90 do
            toggles.[i] <- int (BigInteger.Remainder(n, BigInteger(3)))
            n <- BigInteger.Divide(n, BigInteger(3))
        let notes = ResizeArray<string>()
        let quest =
            if options.[0] <= 3 then Some options.[0]
            else
                notes.Add("Overworld quest is Random in these flags: pick the overworld that matches your seed.")
                None
        let shuffle =
            match options.[5] with   // C06 Item shuffle: None, Item Only, Intra-Dungeon, Full, Random
            | 0 | 1 -> Some 0
            | 2 -> Some 1
            | 3 -> Some 2
            | _ ->
                notes.Add("Item Shuffle is Random in these flags: set Off / Intra / Full to match your seed.")
                None
        Ok { Quest = quest; ItemShuffle = shuffle; Mirror = tri toggles.[5]; HideNumbers = tri toggles.[23]; Notes = List.ofSeq notes }

// what the ZORA string says about the ZORA extras (a three-state flag's Random can't be known from the string)
type ZoraPart = { Progressive : bool
                  FourthSword : Tri
                  FourthInLevel2 : bool
                  BootsReplaceBoomerangs : Tri
                  ExtraRaftBlocks : Tri
                  ExtraPowerBlocks : Tri
                  RandomizeDeadWoods : Tri
                  RandomizeLostHills : Tri }

let decodeZora (s:string) : Result<ZoraPart, string> =
    let s = s.Trim()
    let values = System.Collections.Generic.Dictionary<string,int>()
    let finish() =
        let v name = (match values.TryGetValue name with | true, x -> x | _ -> 0)
        Ok { Progressive = (v "progressive_items" = 1)
             FourthSword = tri (v "add_l4_sword")
             FourthInLevel2 = (v "l4_sword_in_level_2" = 1)
             BootsReplaceBoomerangs = tri (v "pegasus_boots")
             ExtraRaftBlocks = tri (v "extra_raft_blocks")
             ExtraPowerBlocks = tri (v "extra_power_bracelet_blocks")
             RandomizeDeadWoods = tri (v "randomize_dead_woods")
             RandomizeLostHills = tri (v "randomize_lost_hills") }
    if s = "" then finish()
    else
        let dot = s.IndexOf('.')
        let versionOk, version = (if dot > 0 then System.Int32.TryParse(s.Substring(0, dot)) else (false, 0))
        if not versionOk || version < 1 then
            Error "That doesn't look like a ZORA flag string (it should look like 18.abc: a version, a dot, then letters and digits)."
        elif version > zoraCurrentVersion then
            Error (sprintf "These ZORA flags are version %d, newer than this tracker understands (up to %d). Update the tracker." version zoraCurrentVersion)
        else
            match parseBase63 (s.Substring(dot + 1)) with
            | None -> Error "That doesn't look like a ZORA flag string (the part after the dot should use only letters, digits and !)."
            | Some number ->
                let mutable n = number
                for (name, slots, v) in zoraFields do
                    if v <= version then
                        values.[name] <- int (BigInteger.Remainder(n, BigInteger(slots)))
                        n <- BigInteger.Divide(n, BigInteger(slots))
                if not n.IsZero then Error "These ZORA flags have extra digits beyond their version, so they may have been mistyped."
                else finish()
