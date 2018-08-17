namespace ReAction

open System.Collections.Generic
open Types
open Core

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
                return cancel
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
                            do! OnNext x |> obv
                        with ex ->
                            do! OnError ex |> obv
                    | OnError e -> do! OnError e |> obv
                    | OnCompleted -> do! obv OnCompleted
                | None ->
                    ()
            }

        obv, subscribe
