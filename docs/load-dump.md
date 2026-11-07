# `--load` / `--dump`: what the tracker holds after loading a save

```
ZoraTracker.CrossPlatform --load=<save file> --dump=<out file>
```

Loads the save the way the Load Save button does, writes what the tracker then holds to `<out file>` as JSON, and exits. With `--dump` the desktop host uses Avalonia's headless platform (the `Avalonia.Headless` package): no window appears, nothing takes the keyboard focus, and no box is shown. On macOS it takes a few seconds. Run it from a copy of the release folder, since the tracker writes its settings next to the program.

The out file is one JSON object:

| Key | Meaning |
|---|---|
| `warning` | the text of the version warning the load would have shown, or `null` |
| `error` | the load error text if the file failed to load (then `save` is `null`), or `null` |
| `setProblems` | `true` if an item box refused a value in the file (it is left empty; the tracker says nothing otherwise) |
| `save` | the tracker's own save of its state after the load: the format of a manual save, built in memory (nothing is written to `Saves`) |

To check a tool that writes saves: compare `save` with the file that was loaded. A room name the tracker did not know, a box id it refused, or a stairway number used a third time shows as a difference. Some parts always differ (`Timeline`, `OverworldSpotsRemainingOverTime`, and `Blockers`, which the tracker fills in itself).

Code: `Z1R_Avalonia/LoadDump.cs`, with three small hooks in `Program.cs` and `WPFUI.cs`.
