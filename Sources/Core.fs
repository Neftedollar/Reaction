module AsyncReactive.Core

open System.Threading
open AsyncReactive.Types

let disposableEmpty () =
    async {
        return ()
    }

let just (x : 'a) : AsyncObservable<'a> =
    let subscribe (aobv : AsyncObserver<'a>) : Async<AsyncDisposable> =
        let cancellationSource = new CancellationTokenSource()
        let cancel() = async {
            cancellationSource.Cancel()
        }

        let worker = async {
            do! OnNext x |> aobv
            do! OnCompleted |> aobv
        }

        async {
            // Start worker on thread pool
            Async.Start (worker, cancellationSource.Token)
            return cancel
        }
    subscribe

let from xs : AsyncObservable<_> =
    let subscribe (aobv : AsyncObserver<_>) : Async<AsyncDisposable> =
        let cancellationSource = new CancellationTokenSource()
        let cancel() = async {
            cancellationSource.Cancel()
        }

        let worker = async {
            for x in xs do
                do! OnNext x |> aobv

            do! OnCompleted |> aobv
        }

        async {
            // Start worker on thread pool
            Async.Start (worker, cancellationSource.Token)
            return cancel
        }
    subscribe

let map (mapper : AsyncMapper<'a, 'b>) (aobs : AsyncObservable<'a>) : AsyncObservable<'b> =
    let subscribe (aobv : AsyncObserver<'b>) =
        async {
            let _obv n =
                async {
                    match n with
                    | OnNext x ->
                        try
                            let! b = mapper x
                            do! b |> OnNext |> aobv
                        with
                        | ex -> do! OnError ex |> aobv
                    | OnError str -> do! OnError str |> aobv
                    | OnCompleted -> do! aobv OnCompleted

                }
            return! aobs _obv
        }
    subscribe

let filter (predicate : AsyncPredicate<'a>) (aobs : AsyncObservable<'a>) : AsyncObservable<'a> =
    let subscribe (aobv : AsyncObserver<'a>) =
        async {
            let obv n =
                async {
                    match n with
                    | OnNext x ->
                        let! result = predicate x
                        if result then
                            do! x |> OnNext |> aobv
                    | OnError str -> do! OnError str |> aobv
                    | OnCompleted -> do! aobv OnCompleted
                }
            return! aobs obv
        }
    subscribe

