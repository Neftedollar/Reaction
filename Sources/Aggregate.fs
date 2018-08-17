namespace ReAction

open Types
open Core

[<AutoOpen>]
module Aggregate =
    let scanAsync (initial : 's) (accumulator: AsyncAccumulator<'s,'a>) (source : AsyncObservable<'a>) : AsyncObservable<'s> =
        let subscribe (aobv : AsyncObserver<'s>) =
            let mutable state = initial

            async {
                let obv n =
                    async {
                        match n with
                        | OnNext x ->
                            let! state' =  accumulator state x
                            state <- state'
                            do! OnNext state |> aobv
                        | OnError e -> do! OnError e |> aobv
                        | OnCompleted -> do! aobv OnCompleted
                    }
                return! source obv
            }
        subscribe

    let scan (initial : 's) (accumulator: Accumulator<'s,'a>) (source : AsyncObservable<'a>) : AsyncObservable<'s> =
        scanAsync initial (fun s x -> async { return accumulator s x } ) source
