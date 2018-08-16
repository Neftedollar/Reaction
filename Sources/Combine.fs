namespace ReAction

[<AutoOpen>]
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
                                | OnNext x -> do! safeObserver.OnNext x
                                | OnError err ->
                                    do! safeObserver.OnError err
                                    replyChannel.Reply false
                                | OnCompleted -> replyChannel.Reply true
                            }

                        let getInnerSubscription = async {
                            match cmd with
                            | InnerObservable xs ->
                                return! xs.Subscribe (obv replyChannel)
                            | Dispose ->
                                do! innerSubscription.Dispose ()
                                replyChannel.Reply true
                                return disposableEmpty
                        }

                        do! innerSubscription.Dispose ()
                        let! newInnerSubscription = getInnerSubscription
                        return! messageLoop newInnerSubscription
                    }

                    messageLoop disposableEmpty
                )

            async {
                for source in sources do
                    do! innerAgent.PostAndAsyncReply(fun replyChannel -> InnerObservable source, replyChannel) |> Async.Ignore

                do! safeObserver.OnCompleted ()

                let cancel () =
                    async {
                        do! innerAgent.PostAndAsyncReply(fun replyChannel -> Dispose, replyChannel) |> Async.Ignore
                    }
                return AsyncDisposable cancel
            }
        AsyncObservable subscribe

    let startWith (items : seq<'a>) (source : AsyncObservable<'a>) =
        concat [from items; source]

    // Merges an async observable of async observables
    let merge (source : AsyncObservable<AsyncObservable<'a>>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            let safeObserver = safeObserver aobv
            let refCount = refCountActor 1 (async {
                do! safeObserver.OnCompleted ()
            })

            let innerActor =
                let obv n =
                    async {
                        match n with
                        | OnCompleted -> refCount.Post Decrease
                        | _ -> do! safeObserver.Call n
                    }

                MailboxProcessor.Start(fun inbox ->
                    let rec messageLoop (innerSubscriptions : AsyncDisposable list) = async {
                        let! cmd = inbox.Receive()
                        let getInnerSubscriptions = async {
                            match cmd with
                            | InnerObservable xs ->
                                let! inner = xs.Subscribe obv
                                return inner :: innerSubscriptions
                            | Dispose ->
                                for dispose in innerSubscriptions do
                                    do! dispose.Dispose ()
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
                            InnerObservable xs |> innerActor.Post
                        | OnError e -> do! safeObserver.OnError e
                        | OnCompleted -> refCount.Post Decrease
                    }

                let! dispose = source.Subscribe obv
                let cancel () =
                    async {
                        do! dispose.Dispose ()
                        innerActor.Post Dispose
                    }
                return AsyncDisposable cancel
            }
        AsyncObservable subscribe

    type Notifications<'a, 'b> =
    | Source of Notification<'a>
    | Other of Notification<'b>

    let combineLatest (other : AsyncObservable<'b>) (mapper : 'a -> 'b -> 'c) (source : AsyncObservable<'a>) : AsyncObservable<'c> =
        let subscribe (aobv : AsyncObserver<'c>) =
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
                                do! safeObserver.OnError ex
                                return None
                            | OnCompleted ->
                                do! safeObserver.OnCompleted ()
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
                    let c = source' |> Option.bind (fun a -> other' |> Option.map  (fun b -> mapper a b))
                    match c with
                    | Some x -> do! safeObserver.OnNext x
                    | _ -> ()

                    return! messageLoop source' other'
                }

                messageLoop None None
            )

            async {
                let! dispose1 = source.Subscribe (fun (n : Notification<'a>) -> async { Source n |> agent.Post })
                let! dispose2 = other.Subscribe (fun (n : Notification<'b>) -> async { Other n |> agent.Post })

                return compositeDisposable [ dispose1; dispose2 ]
            }
        AsyncObservable subscribe

    let withLatestFrom (other : AsyncObservable<'b>) (mapper : 'a -> 'b -> 'c) (source : AsyncObservable<'a>) : AsyncObservable<'c> =
        let subscribe (aobv : AsyncObserver<'c>) =
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
                                do! safeObserver.OnError ex
                                return None
                            | OnCompleted ->
                                do! safeObserver.OnCompleted ()
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
                    let c = source' |> Option.bind (fun a -> latest' |> Option.map  (fun b -> mapper a b))
                    match c with
                    | Some x -> do! safeObserver.OnNext x
                    | _ -> ()

                    return! messageLoop latest'
                }

                messageLoop None
            )

            async {
                let! dispose1 = source.Subscribe (fun (n : Notification<'a>) -> async { Source n |> agent.Post })
                let! dispose2 = other.Subscribe (fun (n : Notification<'b>) -> async { Other n |> agent.Post })

                return compositeDisposable [ dispose1; dispose2 ]
            }
        AsyncObservable subscribe
