namespace Reaction

open System.Collections.Generic
open System.Threading

open Types
open Core

module Streams =
    /// A hot stream that supports multiple subscribers
    let stream<'a> () : AsyncObserver<'a> * AsyncObservable<'a> =
        let obvs = new List<AsyncObserver<'a>>()

        let subscribe (aobv : AsyncObserver<'a>) : Async<AsyncDisposable> =
            let sobv = safeObserver aobv
            obvs.Add sobv

            async {
                let cancel () = async {
                    obvs.Remove sobv |> ignore
                }
                return cancel
            }

        let obv (n : Notification<'a>) =
            async {
                for aobv in obvs do
                    match n with
                    | OnNext x ->
                        try
                            do! OnNext x |> aobv
                        with ex ->
                            do! OnError ex |> aobv
                    | OnError e -> do! OnError e |> aobv
                    | OnCompleted -> do! aobv OnCompleted
            }

        obv, subscribe

    /// A cold stream that only supports a single subscriber
    let singleStream () : AsyncObserver<'a> * AsyncObservable<'a> =
        let mutable oobv : AsyncObserver<'a> option = None
        let waitTokenSource = new CancellationTokenSource ()

        let subscribe (aobv : AsyncObserver<'a>) : Async<AsyncDisposable> =
            let sobv = safeObserver aobv
            if Option.isSome oobv then
                failwith "singleStream: Already subscribed"

            oobv <- Some sobv
            waitTokenSource.Cancel ()

            async {
                let cancel () = async {
                    oobv <- None
                }
                return cancel
            }

        let obv (n : Notification<'a>) =
            async {
                while oobv.IsNone do
                    // Wait for subscriber
                    Async.StartImmediate (Async.Sleep 100, waitTokenSource.Token)

                match oobv with
                | Some obv ->
                    match n with
                    | OnNext x ->
                        try
                            do! OnNext x |> obv
                        with ex ->
                            do! OnError ex |> obv
                    | OnError e -> do! OnError e |> obv
                    | OnCompleted -> do! obv OnCompleted
                | None ->
                    printfn "No observer for %A" n
                    ()
            }

        obv, subscribe

    let mbStream<'a> () : MailboxProcessor<'a>*AsyncObservable<'a> =
        let obvs = new List<AsyncObserver<'a>>()

        let mb = MailboxProcessor.Start(fun inbox ->
            let rec messageLoop _ = async {
                let! msg = inbox.Receive ()

                for aobv in obvs do
                    try
                        do! OnNext msg |> aobv
                    with ex ->
                        do! OnError ex |> aobv
                return! messageLoop ()
            }
            messageLoop ()
        )

        let subscribe (aobv : AsyncObserver<'a>) : Async<AsyncDisposable> =
            let sobv = safeObserver aobv
            obvs.Add sobv

            async {
                let cancel () = async {
                    obvs.Remove sobv |> ignore
                }
                return cancel
            }

        mb, subscribe
