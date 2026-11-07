using Z1R_Avalonia;
using Newtonsoft.Json;
var conv = new CoopSync.DoorStateAsUnionCaseConverter();
foreach (var s in Enum.GetValues<Dungeon.DoorState>())
{
    var info = new DungeonUI.DoorChangeInfo { Level = 3, X = 5, Y = 2, IsHorizontal = true, NewState = s };
    Console.WriteLine(JsonConvert.SerializeObject(info, conv));
}
// the port reading what WPF wrote
foreach (var line in File.ReadAllLines(args[0]))
{
    var d = JsonConvert.DeserializeObject<CoopSync.DoorChangePayload>(line);
    var info = JsonConvert.DeserializeObject<DungeonUI.DoorChangeInfo>(line, conv);
    Console.WriteLine($"READ {d.NewState.Case} -> {info.NewState} L{info.Level} {info.X},{info.Y} h={info.IsHorizontal}");
}
