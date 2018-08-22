namespace Reaction

open Types
open Core

module Combine =
    // Concatenates an async observable of async observables (WIP)
    let concat (sources : seq<AsyncObservable<'a>>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            let safeObserver = safeObserver aobv

            let innerAgent =
                MailboxProcessor.Start(fun inbox ->
                    let rec messageLoop (innerSubscription : AsyncDisposable) = async {
                        let! cmd, replyChannel = inbox.Receive()

                        let obv (replyChannel : AsyncReplyChannel<bool>) n =
                            async {
                                match n with
                                | OnNext x -> do! OnNext x |> safeObserver
                                | OnError err ->
                                    do! OnError err |> safeObserver
                                    replyChannel.Reply false
                                | OnCompleted -> replyChannel.Reply true
                            }

                        let getInnerSubscription = async {
                            match cmd with
                            | InnerObservable xs ->
                                return! xs (obv <| replyChannel)
                            | Dispose ->
                                do! innerSubscription ()
                                replyChannel.Reply true
                                return disposableEmpty
                        }

                        do! innerSubscription ()
                        let! newInnerSubscription = getInnerSubscription
                        return! messageLoop newInnerSubscription
                    }

                    messageLoop disposableEmpty
                )

            async {
                for source in sources do
                    do! innerAgent.PostAndAsyncReply(fun replyChannel -> InnerObservable source, replyChannel) |> Async.Ignore

                do! safeObserver OnCompleted

                let cancel () =
                    async {
                        do! innerAgent.PostAndAsyncReply(fun replyChannel -> Dispose, replyChannel) |> Async.Ignore
                    }
                return cancel
            }
        subscribe

    let startWith (items : seq<'a>) (source : AsyncObservable<'a>) =
        concat [Creation.ofSeq items; source]

    // Merges an async observable of async observables
    let merge (source : AsyncObservable<AsyncObservable<'a>>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            let safeObserver = safeObserver aobv
            let refCount = refCountAgent 1 (async {
                do! safeObserver OnCompleted
            })

            let innerAgent =
                let obv n =
                    async {
                        match n with
                        | OnCompleted -> refCount.Post Decrease
                        | _ -> do! safeObserver n
                    }

                MailboxProcessor.Start(fun inbox ->
                    let rec messageLoop (innerSubscriptions : AsyncDisposable list) = async {
                        let! cmd = inbox.Receive()
                        let getInnerSubscriptions = async {
                            match cmd with
                            | InnerObservable xs ->
                                let! inner = xs obv
                                return inner :: innerSubscriptions
                            | Dispose ->
                                for dispose in innerSubscriptions do
                                    do! dispose ()
                                return []
                        }
                        let! newInnerSubscriptions = getInnerSubscriptions
                        return! messageLoop newInnerSubscriptions
                    }

                    messageLoop []
                )

            async {
                let obv (ns : Notification<AsyncObservable<'a>>) =
                    async {
                        match ns with
                        | OnNext xs ->
                            refCount.Post Increase
                            InnerObservable xs |> innerAgent.Post
                        | OnError e -> do! OnError e |> safeObserver
                        | OnCompleted -> refCount.Post Decrease
                    }

                let! dispose = source obv
                let cancel () =
                    async {
                        do! dispose ()
                        innerAgent.Post Dispose
                    }
                return cancel
            }
        subscribe

    type Notifications<'a, 'b> =
    | Source of Notification<'a>
    | Other of Notification<'b>

    let combineLatest (other : AsyncObservable<'b>) (source : AsyncObservable<'a>) : AsyncObservable<'a*'b> =
        let subscribe (aobv : AsyncObserver<'a*'b>) =
            let safeObserver = safeObserver aobv

            let agent = MailboxProcessor.Start(fun inbox ->
                let rec messageLoop (source : option<'a>) (other : option<'b>) = async {
                    let! cn = inbox.Receive()

                    let onNextOption n =
                        async {
                            match n with
                            | OnNext x ->
                                return Some x
                            | OnError ex ->
                                do! OnError ex |> safeObserver
                                return None
                            | OnCompleted ->
                                do! OnCompleted |> safeObserver
                                return None
                        }

                    let! source', other' = async {
                        match cn with
                        | Source n ->
                            let! onNextOptionN = onNextOption n
                            return onNextOptionN, other
                        | Other n ->
                            let! onNextOptionN = onNextOption n
                            return source, onNextOptionN
                    }
                    let c = source' |> Option.bind (fun a -> other' |> Option.map  (fun b -> a, b))
                    match c with
                    | Some x -> do! OnNext x |> safeObserver
                    | _ -> ()

                    return! messageLoop source' other'
                }

                messageLoop None None
            )

            async {
                let! dispose1 = source (fun (n : Notification<'a>) -> async { Source n |> agent.Post })
                let! dispose2 = other (fun (n : Notification<'b>) -> async { Other n |> agent.Post })

                return compositeDisposable [ dispose1; dispose2 ]
            }
        subscribe

    let withLatestFrom (other : AsyncObservable<'b>) (source : AsyncObservable<'a>) : AsyncObservable<'a*'b> =
        let subscribe (aobv : AsyncObserver<'a*'b>) =
            let safeObserver = safeObserver aobv

            let agent = MailboxProcessor.Start(fun inbox ->
                let rec messageLoop (latest : option<'b>) = async {
                    let! cn = inbox.Receive()

                    let onNextOption n =
                        async {
                            match n with
                            | OnNext x ->
                                return Some x
                            | OnError ex ->
                                do! OnError ex |> safeObserver
                                return None
                            | OnCompleted ->
                                do! OnCompleted |> safeObserver
                                return None
                        }

                    let! source', latest' = async {
                        match cn with
                        | Source n ->
                            let! onNextOptionN = onNextOption n
                            return onNextOptionN, latest
                        | Other n ->
                            let! onNextOptionN = onNextOption n
                            return None, onNextOptionN
                    }
                    let c = source' |> Option.bind (fun a -> latest' |> Option.map  (fun b -> a, b))
                    match c with
                    | Some x -> do! OnNext x |> safeObserver
                    | _ -> ()

                    return! messageLoop latest'
                }

                messageLoop None
            )

            async {
                let! dispose1 = source (fun (n : Notification<'a>) -> async { Source n |> agent.Post })
                let! dispose2 = other (fun (n : Notification<'b>) -> async { Other n |> agent.Post })

                return compositeDisposable [ dispose1; dispose2 ]
            }
        subscribe
