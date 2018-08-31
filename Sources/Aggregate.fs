namespace Reaction

open Types
open Core

module Aggregate =
    let scanAsync (initial : 's) (accumulator: 's -> 'a -> Async<'s>) (source : AsyncObservable<'a>) : AsyncObservable<'s> =
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

    let scan (initial : 's) (accumulator: 's -> 'a -> 's) (source : AsyncObservable<'a>) : AsyncObservable<'s> =
        scanAsync initial (fun s x -> async { return accumulator s x } ) source

    let groupBy (groupSelector: 'a -> 'g) (source : AsyncObservable<'a>) : AsyncObservable<AsyncObservable<'a>> =
        let subscribe (aobv : AsyncObserver<AsyncObservable<'a>>) =
            let agent = MailboxProcessor.Start(fun inbox ->
                let rec messageLoop (groups: Map<'g, AsyncObserver<'a>>) = async {
                    let! n = inbox.Receive ()

                    let! newGroups =
                        async {
                            match n with
                            | OnNext x ->
                                let groupKey = groupSelector x
                                let! newGroups = async {
                                    match groups.TryFind groupKey with
                                    | Some group ->
                                        do! group n
                                        return groups
                                    | None ->
                                        let obv, obs = Streams.stream ()
                                        return groups.Add (groupKey, obv)
                                }
                                return newGroups
                            | OnError ex ->
                                for entry in groups do
                                    do! OnError ex |> entry.Value
                                do! OnError ex |> aobv
                                return Map.empty
                            | OnCompleted ->
                                for entry in groups do
                                    do! OnCompleted |> entry.Value
                                do! aobv OnCompleted
                                return Map.empty
                        }

                    return! messageLoop newGroups
                }

                messageLoop Map.empty
            )

            async {
                let obv (n : Notification<'a>) =
                    async {
                        agent.Post n
                    }

                return! source obv
            }
        subscribe