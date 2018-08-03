module Tests.Utils

open System.Collections.Generic
open System.Threading;

open System.Threading.Tasks
open System.Collections.Concurrent
open AsyncReactive.Types
open System

type TestObserver<'a>() =
    let notifications = new List<Notification<'a>>()

    member this.Notifications = notifications

    member this.OnNext (x : Notification<'a>) =
        async {
            do! Async.Sleep 1 // Make it possible to cancel
            this.Notifications.Add(x)
        }
exception InnerError of string

type TestScheduler() =
    inherit SynchronizationContext()

    let queue = new BlockingCollection<Tuple<SendOrPostCallback,Object>>()

    member this.Queue = queue

    override this.Post(d : SendOrPostCallback, state: Object) =
        printfn "Post %A" d
        //this.Queue.Add((d, state))
        d.Invoke state

    member this.RunOnCurrentThread() =
        printfn "RunOnCurrentThread"
        let mutable workItem : Tuple<SendOrPostCallback, Object> = (null, null)

        while (this.Queue.TryTake(ref workItem, Timeout.Infinite)) do
            printfn "Got workitem"
            let a, b = workItem
            a.Invoke b
        ()

    member this.Complete() =
        printfn "Complete"
        queue.CompleteAdding ()

    static member Run(func: Func<Task>) =
        printfn "Run"
        let prevCtx = SynchronizationContext.Current

        try
            let syncCtx = new TestScheduler()

            SynchronizationContext.SetSynchronizationContext(syncCtx)
            printfn "Invoke"
            let t = func.Invoke ()

            t.ContinueWith(fun _ -> syncCtx.Complete(), TaskScheduler.Default) |> ignore

            syncCtx.RunOnCurrentThread()
            t.GetAwaiter().GetResult()

        finally
            printf "Finally"
            SynchronizationContext.SetSynchronizationContext(prevCtx)

