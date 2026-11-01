module Z1R_Avalonia_Desktop.Program

open Avalonia

[<EntryPoint>]
let main argv =
    AppBuilder.Configure<Z1R_Avalonia.Prototype.App>().UsePlatformDetect().StartWithClassicDesktopLifetime(argv)
