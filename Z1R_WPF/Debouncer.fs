module Debouncer

type Debouncer(delayMs: int) =
    let mutable currentCts : System.Threading.CancellationTokenSource option = None

    member this.Trigger(action: unit -> unit) =
        currentCts |> Option.iter (fun cts -> cts.Cancel())
        let newCts = new System.Threading.CancellationTokenSource()
        currentCts <- Some newCts

        async {
            do! Async.Sleep delayMs
            if not newCts.IsCancellationRequested then
                action()
        } |> Async.Start
