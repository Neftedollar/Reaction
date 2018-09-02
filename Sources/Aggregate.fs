namespace Reaction

open System.Threading
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

    let groupBy (keyMapper: 'a -> 'g) (source : AsyncObservable<'a>) : AsyncObservable<AsyncObservable<'a>> =
        let subscribe (aobv : AsyncObserver<AsyncObservable<'a>>) =
            let cancellationSource = new CancellationTokenSource()
            let agent = MailboxProcessor.Start((fun inbox ->
                let rec messageLoop ((groups, disposed) : Map<'g, AsyncObserver<'a>>*bool) = async {
                    let! n = inbox.Receive ()

                    if disposed then
                        return! messageLoop (Map.empty, true)

                    let! newGroups, disposed =
                        async {
                            match n with
                            | OnNext x ->
                                let groupKey = keyMapper x
                                let! newGroups = async {
                                    match groups.TryFind groupKey with
                                    | Some group ->
                                        //printfn "Found group: %A" groupKey
                                        do! group n
                                        return groups, false
                                    | None ->
                                        //printfn "New group: %A" groupKey
                                        let obv, obs = Streams.singleStream ()
                                        do! OnNext obs |> aobv
                                        do! obv n
                                        return groups.Add (groupKey, obv), false
                                }
                                return newGroups
                            | OnError ex ->
                                //printfn "%A" n

                                for entry in groups do
                                    do! OnError ex |> entry.Value
                                do! OnError ex |> aobv
                                return Map.empty, true
                            | OnCompleted ->
                                //printfn "%A" n

                                for entry in groups do
                                    do! OnCompleted |> entry.Value
                                do! aobv OnCompleted
                                return Map.empty, true
                        }

                    return! messageLoop (newGroups, disposed)
                }

                messageLoop (Map.empty, false)), cancellationSource.Token)

            async {
                let obv (n : Notification<'a>) =
                    async {
                        agent.Post n
                    }
                let! subscription = source obv
                let cancel () = async {
                    do! subscription ()
                    cancellationSource.Cancel()
                }
                return cancel
            }
        subscribe