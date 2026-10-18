module Z1R_Avalonia_Browser.Program

open System.Runtime.Versioning
open System.Threading.Tasks
open Avalonia
open Avalonia.Browser

[<assembly: SupportedOSPlatform("browser")>]
do ()

[<EntryPoint>]
let main _ =
    // starts the shared App inside the <div id="out"> from AppBundle/index.html
    AppBuilder.Configure<Z1R_Avalonia.Program.App>()
        .StartBrowserAppAsync("out")
        .GetAwaiter().GetResult()
    0
