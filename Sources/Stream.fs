namespace ReAction

open System.Collections.Generic

[<AutoOpen>]
module Streams =
     // Hot stream that supports multiple subscribers
    let stream<'a> () : AsyncObserver<'a> * AsyncObservable<'a> =
        let obvs = new List<AsyncObserver<'a>>()

        let subscribe (aobv : AsyncObserver<'a>) : Async<AsyncDisposable> =
            let sobv = safeObserver aobv
            obvs.Add(sobv)

            async {
                let cancel() = async {
                    obvs.Remove(sobv)|> ignore
                }
                return AsyncDisposable cancel
            }

        let obv (n : Notification<'a>) =
            async {
                for aobv in obvs do
                    match n with
                    | OnNext x ->
                        try
                            do! aobv.OnNext x
                        with ex ->
                            do! aobv.OnError ex
                    | OnError e -> do! aobv.OnError e
                    | OnCompleted -> do! aobv.OnCompleted ()
            }

        AsyncObserver obv, AsyncObservable subscribe

    // Cold stream that only supports a single subscriber
    let singleStream () : AsyncObserver<'a> * AsyncObservable<'a> =
        let mutable oobv : AsyncObserver<'a> option = None

        let subscribe (aobv : AsyncObserver<'a>) : Async<AsyncDisposable> =
            let sobv = safeObserver aobv
            if Option.isSome oobv then
                failwith "Already subscribed"

            oobv <- Some sobv

            async {
                let cancel () = async {
                    oobv <- None
                }
                return AsyncDisposable cancel
            }

        let obv (n : Notification<'a>) =
            async {
                while Option.isNone oobv do
                    do! Async.Sleep 100  // Works with Fable

                match oobv with
                | Some obv ->
                    match n with
                    | OnNext x ->
                        try
                            do! obv.OnNext x
                        with ex ->
                            do! obv.OnError ex
                    | OnError e -> do! obv.OnError e
                    | OnCompleted -> do! obv.OnCompleted ()
                | None ->
                    ()
            }

        AsyncObserver obv, AsyncObservable subscribe
