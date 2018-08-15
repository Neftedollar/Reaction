namespace ReAction

open System.Collections.Generic
open System.Threading
open System

[<AutoOpen>]
module Core =
    let disposableEmpty () =
        async {
            return ()
        }

    let compositeDisposable (ds : AsyncDisposable seq) : AsyncDisposable =
        let cancel () = async {
            for d in ds do
                do! d ()
        }
        cancel

    let canceller () =
        let cancellationSource = new CancellationTokenSource()
        let cancel() = async {
            cancellationSource.Cancel()
            ()
        }

        cancel, cancellationSource.Token


    /// Safe observer that wraps the given observer and makes sure that
    /// the Rx grammar (onNext* (onError|onCompleted)?) is not violated.
    let safeObserver (obv : AsyncObserver<'a>) =
        let agent = MailboxProcessor.Start(fun inbox ->
            let rec messageLoop stopped = async {
                let! n = inbox.Receive()

                if stopped then
                    return! messageLoop stopped

                let! stop = async {
                    match n with
                    | OnNext x ->
                        try
                            do! OnNext x |> obv
                            return false
                        with
                        | ex ->
                            do! OnError ex |> obv
                            return true
                    | _ ->
                        do! obv n
                        return true
                }

                return! messageLoop stop
            }

            messageLoop false
        )
        let safeObv (n : Notification<'a>) = async {
            agent.Post n
        }
        safeObv

    // Create async observable from async worker function
    let fromAsync (worker : AsyncObserver<'a> -> CancellationToken -> Async<unit>) : AsyncObservable<_> =
        let subscribe (aobv : AsyncObserver<_>) : Async<AsyncDisposable> =
            let cancel, token = canceller ()
            let obv = safeObserver aobv

            async {
                let! _ = Async.StartChild (worker obv token, 0)
                return cancel
            }
        subscribe

    // An async observervable that just completes when subscribed.
    let empty () : AsyncObservable<'a> =
        fromAsync (fun obv _ -> async {
            do! OnCompleted |> obv
        })

    // An async observervable that just fails with an error when subscribed.
    let fail (exn) : AsyncObservable<'a> =
        fromAsync (fun obv _ -> async {
            do! OnError exn |> obv
        })

    let from (xs : seq<'a>) : AsyncObservable<'a> =
        fromAsync (fun obv token -> async {
            for x in xs do
                if token.IsCancellationRequested then
                    raise (OperationCanceledException("Operation cancelled"))

                try
                    do! OnNext x |> obv
                with ex ->
                    do! OnError ex |> obv

            do! OnCompleted |> obv
        })

    let just (x : 'a) : AsyncObservable<'a> =
        from [ x ]

    // Create an async observable from a subscribe function. So trivial
    // we should remove it once we get used to the idea that subscribe is
    // exactly the same as an async observable.
    let create (subscribe : AsyncObserver<_> -> Async<AsyncDisposable>) : AsyncObservable<_> =
        subscribe

    // The classic map (select) operator with async mapper
    let mapAsync (mapper : AsyncMapper<'a,'b>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        let subscribe (aobv : AsyncObserver<'b>) =
            async {
                let _obv n =
                    async {
                        match n with
                        | OnNext x ->
                            let! b =  mapper x
                            do! b |> OnNext |> aobv  // Let exceptions bubble to the top
                        | OnError ex -> do! OnError ex |> aobv
                        | OnCompleted -> do! aobv OnCompleted

                    }
                return! source _obv
            }
        subscribe

    // The classic map (select) operator with sync mapper
    let inline map (mapper : Mapper<'a, 'b>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        mapAsync (fun x -> async { return mapper x }) source

    // The classic map (select) operator with an indexed and async mapper
    let mapIndexedAsync (mapper : AsyncMapperIndexed<'a, 'b>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        let infinite = Seq.initInfinite (fun index -> index)
        let indexer = infinite.GetEnumerator ()

        mapAsync (fun x -> async {
                    indexer.MoveNext () |> ignore
                    let index = indexer.Current
                    return! mapper x index
                  }) source

    // The classic map (select) operator with sync and indexed mapper
    let inline mapIndexed (mapper : MapperIndexed<'a, 'b>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        mapIndexedAsync (fun x i -> async { return mapper x i }) source

    // The classic filter (where) operator with async predicate
    let filterAsync (predicate : AsyncPredicate<'a>) (source : AsyncObservable<'a>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            async {
                let obv n =
                    async {
                        match n with
                        | OnNext x ->
                            let! result = predicate x
                            if result then
                                do! x |> OnNext |> aobv  // Let exceptions bubble to the top
                        | _ -> do! aobv n
                    }
                return! source obv
            }
        subscribe

    // The classic filter (where) operator with sync predicate
    let inline filter (predicate : Re.Predicate<'a>) (source : AsyncObservable<'a>) : AsyncObservable<'a> =
        filterAsync (fun x -> async { return predicate x }) source

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

    // Observer that forwards notifications to a given actor
    let actorObserver (agent : MailboxProcessor<Notification<'a>>) =
        let obv n =
            async {
                agent.Post n
            }
        obv

    let refCountActor initial action =
        MailboxProcessor.Start(fun inbox ->
            let rec messageLoop count = async {
                let! cmd = inbox.Receive()
                let newCount =
                    match cmd with
                    | Increase -> count + 1
                    | Decrease -> count - 1

                if newCount = 0 then
                    do! action
                    return ()

                return! messageLoop newCount
            }

            messageLoop initial
        )

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
        concat [from items; source]

    // Merges an async observable of async observables
    let merge (source : AsyncObservable<AsyncObservable<'a>>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            let safeObserver = safeObserver aobv
            let refCount = refCountActor 1 (async {
                do! safeObserver OnCompleted
            })

            let innerActor =
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
                            InnerObservable xs |> innerActor.Post
                        | OnError e -> do! OnError e |> safeObserver
                        | OnCompleted -> refCount.Post Decrease
                    }

                let! dispose = source obv
                let cancel () =
                    async {
                        do! dispose ()
                        innerActor.Post Dispose
                    }
                return cancel
            }
        subscribe

    // The classic flap map (selectMany, bind, mapMerge) operator
    let flatMap (mapper : Mapper<'a, AsyncObservable<'b>>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        source |> map mapper |> merge

    let flatMapIndexed (mapper : MapperIndexed<'a, AsyncObservable<'b>>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        source |> mapIndexed mapper |> merge

    let flatMapAsync (mapper : AsyncMapper<'a, AsyncObservable<'b>>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        source |> mapAsync mapper |> merge

    let flatMapIndexedAsync (mapper : AsyncMapperIndexed<'a, AsyncObservable<'b>>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        source |> mapIndexedAsync mapper |> merge

    // Delays each notification with the given number of milliseconds
    let delay (msecs : int) (source : AsyncObservable<'a>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            let agent = MailboxProcessor.Start(fun inbox ->
                let rec messageLoop state = async {
                    let! n, dueTime = inbox.Receive()

                    let diff : TimeSpan = dueTime - DateTime.Now
                    let msecs = Convert.ToInt32 diff.TotalMilliseconds
                    if msecs > 0 then
                        do! Async.Sleep msecs
                    do! aobv n

                    return! messageLoop state
                }

                messageLoop (0, 0)
            )

            async {
                let obv n =
                    async {
                        let dueTime = DateTime.Now + TimeSpan.FromMilliseconds(float msecs)
                        agent.Post (n, dueTime)
                    }
                return! source obv
            }
        subscribe

    let distinctUntilChanged (source : AsyncObservable<'a>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            let safeObserver = safeObserver aobv
            let agent = MailboxProcessor.Start(fun inbox ->
                let rec messageLoop (latest : Notification<'a>) = async {
                    let! n = inbox.Receive()

                    let! latest' = async {
                        match n with
                        | OnNext x ->
                            if n <> latest then
                                try
                                    do! OnNext x |> safeObserver
                                with
                                | ex -> do! OnError ex |> safeObserver
                        | _ ->
                            do! safeObserver n
                        return n
                    }

                    return! messageLoop latest'
                }

                messageLoop OnCompleted // Use as sentinel value as it will not match any OnNext value
            )

            async {
                let obv n =
                    async {
                        agent.Post n
                    }
                return! source obv
            }
        subscribe

    let debounce msecs (source : AsyncObservable<'a>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            let safeObserver = safeObserver aobv
            let infinite = Seq.initInfinite (fun index -> index)

            let agent = MailboxProcessor.Start(fun inbox ->
                let rec messageLoop currentIndex = async {
                    let! n, index = inbox.Receive ()

                    let! newIndex = async {
                        match n, index with
                        | OnNext _, idx when idx = currentIndex ->
                            do! safeObserver n
                            return index
                        | OnNext _, _ ->
                            if index > currentIndex then
                                return index
                            else
                                return currentIndex

                        | _, _ ->
                            do! safeObserver n
                            return currentIndex
                    }
                    return! messageLoop newIndex
                }

                messageLoop -1
            )

            async {
                let indexer = infinite.GetEnumerator ()

                let obv (n : Notification<'a>) =
                    async {
                        indexer.MoveNext () |> ignore
                        let index = indexer.Current
                        agent.Post (n, index)

                        let worker = async {
                            do! Async.Sleep msecs
                            agent.Post (n, index)
                        }

                        let! _ = Async.StartChild worker
                        ()
                    }
                let! dispose = source obv

                let cancel () =
                    async {
                        do! dispose ()
                        agent.Post (OnCompleted, 0)
                    }
                return cancel
            }
        subscribe

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
                    let c = source' |> Option.bind (fun a -> other' |> Option.map  (fun b -> mapper a b))
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