module DungeonSync

open System.Collections.Generic
open System.Security.Cryptography
open System.Text
open Newtonsoft.Json

let private hashDungeonModel (dm: DungeonSaveAndLoad.DungeonModel) =
    let json = JsonConvert.SerializeObject(dm, Formatting.None)
    using (SHA256.Create()) (fun sha ->
        let bytes = Encoding.UTF8.GetBytes(json)
        let hash = sha.ComputeHash(bytes)
        System.Convert.ToBase64String(hash))

let private lastReceivedHashPerSenderPerLevel = Dictionary<(int * string), string>()
let private lastSentHashPerLevel = Dictionary<int, string>()

let shouldApplyUpdate (level: int) (senderId: string) (incomingHash: string) =
    let key = (level, senderId)
    let alreadyApplied =
        match lastReceivedHashPerSenderPerLevel.TryGetValue key with
        | true, existing when existing = incomingHash -> true
        | _ -> false
    if not alreadyApplied then
        lastReceivedHashPerSenderPerLevel.[key] <- incomingHash
    not alreadyApplied

let shouldSendUpdate (level: int) (outgoingHash: string) =
    match lastSentHashPerLevel.TryGetValue level with
    | true, prev when prev = outgoingHash -> false
    | _ ->
        lastSentHashPerLevel.[level] <- outgoingHash
        true

let makeDungeonUpdatePayload (level: int) (dm: DungeonSaveAndLoad.DungeonModel) (myConsoleId: string) =
    let hash = hashDungeonModel dm
    {| Level = level; SenderId = myConsoleId; Hash = hash; DungeonModel = dm |}
