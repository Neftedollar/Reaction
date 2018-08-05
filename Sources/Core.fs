namespace AsyncReactive

open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
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
            let cancel, token = canceller ()
            let obv = safeObserver aobv
            let worker = async {
                do! OnCompleted |> obv
            }

            async {
                // Start value generating worker on thread pool
                Async.Start (worker, token)
                return cancel
            }
        subscribe

    let just (x : 'a) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) : Async<AsyncDisposable> =
            let cancel, token = canceller ()
            let obv = safeObserver aobv
            let worker = async {
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

    let map (amapper : AsyncMapper<'a, 'b>) (aobs : AsyncObservable<_>) : AsyncObservable<_> =
        let subscribe (aobv : AsyncObserver<'b>) =
            async {
                let _obv n =
                    async {
                        match n with
                        | OnNext x ->
                            let! b = amapper x
                            do! b |> OnNext |> aobv  // Let exceptions bubble to the top
                        | OnError str -> do! OnError str |> aobv
                        | OnCompleted -> do! aobv OnCompleted

                    }
                return! aobs _obv
            }
        subscribe

    let filter (apredicate : AsyncPredicate<'a>) (aobs : AsyncObservable<_>) : AsyncObservable<_> =
        let subscribe (aobv : AsyncObserver<'a>) =
            async {
                let obv n =
                    async {
                        match n with
                        | OnNext x ->
                            let! result = apredicate x
                            if result then
                                do! x |> OnNext |> aobv  // Let exceptions bubble to the top
                        | OnError str -> do! OnError str |> aobv
                        | OnCompleted -> do! aobv OnCompleted
                    }
                return! aobs obv
            }
        subscribe

    let scan (initial : 's) (accumulator: AsyncAccumulator<'s,'a>) (aobs : AsyncObservable<'a>) : AsyncObservable<'s> =
        let subscribe (aobv : AsyncObserver<'s>) =
            let mutable state = initial

            async {
                let obv n =
                    async {
                        match n with
                        | OnNext x ->
                            let! state' =  accumulator initial x
                            state <- state'
                            do! OnNext state |> aobv
                        | OnError e -> do! OnError e |> aobv
                        | OnCompleted -> do! aobv OnCompleted
                    }
                return! aobs obv
            }
        subscribe

    let stream () : AsyncObserver<'a> * AsyncObservable<'a> =
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

    let singleStream () : AsyncObserver<'a> * AsyncObservable<'a> =
        let mutable oobv : AsyncObserver<'a> option = None
        let wait = TaskCompletionSource<unit>()

        let subscribe (aobv : AsyncObserver<'a>) : Async<AsyncDisposable> =
            let sobv = safeObserver aobv
            oobv <- Some sobv
            wait.SetResult()

            async {
                let cancel() = async {
                    oobv <- None
                }
                return cancel
            }

        let obv (n : Notification<'a>) =
            async {
                if Option.isNone oobv then
                    do! Async.AwaitTask wait.Task

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

    let merge (aobs : AsyncObservable<AsyncObservable<'a>>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            let innerSubscriptions = ResizeArray<AsyncDisposable>()
            let mutable refCount = 1
            let monitor = new Object()

            let iobv n =
                async {
                    let notifier = async {
                        match n with
                        | OnNext x ->
                            //printfn "OnNext %A" x
                            do! OnNext x |> aobv
                        | OnError e -> do! OnError e |> aobv
                        | OnCompleted ->
                            refCount <- refCount - 1
                            if refCount = 0 then
                                do! aobv OnCompleted
                    }

                    // Make sure we serialize notifications to the observer
                    lock monitor (fun () ->
                        Async.StartImmediate notifier
                    )
                }

            async {
                let obv (ns : Notification<AsyncObservable<'a>>) =
                    async {
                        match ns with
                        | OnNext xs ->
                            let! inner = xs iobv
                            innerSubscriptions.Add(inner)
                            refCount <- refCount + 1

                        | OnError e -> do! OnError e |> aobv
                        | OnCompleted ->
                            refCount <- refCount - 1
                            if refCount = 0 then
                                do! aobv OnCompleted
                    }
                let! subscription = aobs obv
                let cancel () =
                    async {
                        do! subscription ()
                        for inner in innerSubscriptions do
                            do! inner ()
                    }
                return cancel
            }
        subscribe

    let flatMap (amapper : 'a -> Async<AsyncObservable<'b>>) (aobs : AsyncObservable<'a>) : AsyncObservable<'b> =
        aobs |> map amapper |> merge