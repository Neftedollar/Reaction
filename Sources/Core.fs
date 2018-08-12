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

    let safeObserver(obv: AsyncObserver<'t>) =
        let mutable stopped = false

        let wrapped (x : Notification<'t>)  =
            async {
                if not stopped then
                    match x with
                    | OnNext n -> do! OnNext n |> obv
                    | OnError e ->
                        stopped <- true
                        do! OnError e |> obv
                    | OnCompleted ->
                        stopped <- true
                        do! obv OnCompleted
            }
        wrapped

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

    // The classic map (select) operator with async and indexed mapper
    let mapIndexedAsync (mapper : AsyncMapperIndexed<'a, 'b>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        let mutable index = 0
        mapAsync (fun x -> async {
                    let index' = index
                    index <- index + 1
                    return! mapper x index'
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

    // Actor that forwards notification to a given observer
    let observerActor obv =
        MailboxProcessor.Start(fun inbox ->
            let rec messageLoop stopped = async {
                let! n = inbox.Receive()
                let stop =
                    match n with
                    | OnNext n -> stopped
                    | _ -> true
                if not stopped then
                    do! obv n

                return! messageLoop stop
            }

            messageLoop false
        )

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
            let safeObserver = observerActor aobv

            let innerAgent =
                MailboxProcessor.Start(fun inbox ->
                    let rec messageLoop (innerSubscription : AsyncDisposable) = async {
                        let! cmd, replyChannel = inbox.Receive()

                        let obv (replyChannel : AsyncReplyChannel<bool>) n =
                            async {
                                match n with
                                | OnCompleted -> replyChannel.Reply true
                                | _ -> safeObserver.Post n
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

                safeObserver.Post OnCompleted

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
            let safeObserver = observerActor aobv
            let refCount = refCountActor 1 (async {
                safeObserver.Post OnCompleted
            })

            let innerActor =
                let obv n =
                    async {
                        match n with
                        | OnCompleted -> refCount.Post Decrease
                        | _ -> safeObserver.Post n
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
                        | OnError e -> OnError e |> safeObserver.Post
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
            let safeObserver = observerActor aobv
            let agent = MailboxProcessor.Start(fun inbox ->
                let rec messageLoop (latest : Notification<'a>) = async {
                    let! n = inbox.Receive()

                    let latest' =
                        match n with
                        | OnNext x ->
                            if n <> latest then
                                try
                                    OnNext x |> safeObserver.Post
                                with
                                | ex -> OnError ex |> safeObserver.Post
                        | _ ->
                            safeObserver.Post n
                        n

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
            let safeObserver = observerActor aobv

            let agent = MailboxProcessor.Start(fun inbox ->
                let rec messageLoop currentIndex = async {
                    let! n, index = inbox.Receive()

                    let newIndex =
                        match n, index with
                        | OnNext _, idx when idx = currentIndex ->
                            safeObserver.Post n
                            index
                        | OnNext _, _ ->
                            if index > currentIndex then
                                index
                            else
                                currentIndex

                        | _, _ ->
                            printfn "%A" n
                            safeObserver.Post n
                            currentIndex

                    return! messageLoop newIndex
                }

                messageLoop -1
            )

            async {
                let indexer = Seq.initInfinite (fun index -> index) |> (fun x -> x.GetEnumerator ())

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