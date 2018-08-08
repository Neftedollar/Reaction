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

    // An async observervable that just completes when subscribed.
    let empty () : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) : Async<AsyncDisposable> =
            let obv = safeObserver aobv

            async {
                Async.Start (async {
                    do! OnCompleted |> obv
                })
                return disposableEmpty
            }
        subscribe

    // An async observervable that just fails with an error when subscribed.
    let fail (exn) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) : Async<AsyncDisposable> =
            let obv = safeObserver aobv

            async {
                Async.Start (async {
                    do! OnError exn |> obv
                })
                return disposableEmpty
            }
        subscribe

    let from xs : AsyncObservable<_> =
        let subscribe (aobv : AsyncObserver<_>) : Async<AsyncDisposable> =
            let cancel, token = canceller ()
            let obv = safeObserver aobv
            let worker = async {
                for x in xs do
                    try
                        do! OnNext x |> obv
                    with ex ->
                        do! OnError ex |> obv

                do! OnCompleted |> obv
            }

            async {
                // Start value generating worker on thread pool
                Async.Start (worker, token)
                return cancel
            }
        subscribe

    let just (x : 'a) : AsyncObservable<'a> =
        from [ x ]

    // Create an async observable from a subscribe function. So trivial
    // we should remove it once we get used to the idea that subscribe is
    // exactly the same as an async observable.
    let create (subscribe : AsyncObserver<_> -> Async<AsyncDisposable>) : AsyncObservable<_> =
        subscribe

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

    // The classic map (select) operator
    let inline map (mapper : Mapper<'a, 'b>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        mapAsync (fun x -> async { return mapper x }) source

    let mapAsyncIndexed (mapper : AsyncMapperIndexed<'a, 'b>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        let mutable index = 0
        mapAsync (fun x -> async {
                        let index' = index
                        index <- index + 1
                        return! mapper x index'
                  }) source

    let inline mapIndexed (mapper : MapperIndexed<'a, 'b>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        mapAsyncIndexed (fun x i -> async { return mapper x i }) source

    // The classic filter (where) operator
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

    // The classic filter (where) operator
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
                    | OnNext n ->
                        stopped
                    | _ ->
                        true
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
                    | Increase ->
                        count + 1
                    | Decrease ->
                        count - 1

                if newCount = 0 then
                    do! action
                    return ()

                return! messageLoop newCount
            }

            messageLoop initial
        )

    // Concatenates an async observable of async observables (WIP)
    let concat (source : AsyncObservable<AsyncObservable<'a>>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            async {
                return disposableEmpty
            }
        subscribe

    type InnerSubscriptionCmd<'a> =
        | NewObservable of AsyncObservable<'a>
        | Dispose

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
                            | NewObservable xs ->
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
                            NewObservable xs |> innerActor.Post
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

    let flatMapAsyncIndexed (mapper : AsyncMapperIndexed<'a, AsyncObservable<'b>>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        source |> mapAsyncIndexed mapper |> merge

    // Delays each notification with the given number of milliseconds
    let delay (milliseconds : float) (source : AsyncObservable<_>) : AsyncObservable<'a> =
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

                messageLoop (0,0)
            )

            async {
                let obv n =
                    async {
                        let dueTime = DateTime.Now + TimeSpan.FromMilliseconds(milliseconds)
                        agent.Post (n, dueTime)
                    }
                return! source obv
            }
        subscribe