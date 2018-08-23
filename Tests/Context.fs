module Reaction.Context

open System
open System.Collections.Concurrent
open System.Threading;
open System.Threading.Tasks

// A single thread test synchronization context.
// Inspored by: https://blogs.msdn.microsoft.com/pfxteam/2012/01/20/await-synchronizationcontext-and-console-apps/
type TestSynchronizationContext () =
    inherit SynchronizationContext ()

    let mutable delayed : List<DateTime*SendOrPostCallback*Object> = List.empty
    let ready = new BlockingCollection<SendOrPostCallback*Object> ()
    let now = DateTime.Now

    member val private Delayed = delayed with get, set
    member val private Running = false with get, set
    member private this.Ready = ready

    member val Now = now with get, set

    member this.SleepAsync (msecs: int) =
        printfn "SleepAsync: %d" msecs
        let task = new TaskCompletionSource<unit> ()

        let action (_ : Object) =
            task.SetResult ()

        async {
            let timeout = TimeSpan.FromMilliseconds (float msecs)
            let dueTime = this.Now + timeout

            lock this.Delayed (fun () ->
                let workItem = (dueTime, SendOrPostCallback action, null) :: this.Delayed
                this.Delayed <- List.sortBy (fun (x, _, _) -> x) workItem
            )

            return! Async.AwaitTask task.Task
        }

    override this.Post(d : SendOrPostCallback, state: Object) =
        printfn "Post %A" d
        if this.Running then
            this.Ready.Add((d, state))
        else
            d.Invoke state

    /// Process delayed tasks and advances time
    member private this.ProcessDelayed () =
        if this.Ready.Count = 0 then
            lock this.Delayed (fun () ->
                match this.Delayed with
                | (x, y, z) :: rest ->
                    this.Ready.Add ((y, z))
                    this.Delayed <- rest
                    this.Now <- x
                | [] -> ()
            )

    /// Process ready queue
    member private this.ProcessReady() =
        printfn "ProcessReady"
        let workItem = ref (SendOrPostCallback (fun _ -> ()), null)

        this.ProcessDelayed ()

        while (this.Ready.TryTake (workItem, Timeout.Infinite)) do
            printfn "Got workitem: %A" workItem
            let a, b = !workItem
            a.Invoke b

            this.ProcessDelayed ()
        ()

    /// Run async function until completion
    member this.RunAsync(func: Async<unit>) = async {
        let cts = new CancellationTokenSource ()
        let prevCtx = SynchronizationContext.Current
        do! Async.SwitchToContext this

        this.Running <- true

        Async.StartWithContinuations(
            func,
            (fun cont ->
                printfn "cont-%A" cont
                this.Ready.CompleteAdding ()),
            (fun exn ->
                this.Ready.CompleteAdding ()
                printfn "exception-%s" <| exn.ToString()),
            (fun exn ->
                this.Ready.CompleteAdding ()
                printfn "cancell-%s" <| exn.ToString()),
             cts.Token
        )

        try
            this.ProcessReady ()
        with
        | exn -> printfn "Exception-%s" <| exn.ToString ()

        this.Running <- false
        do! Async.SwitchToContext prevCtx
}