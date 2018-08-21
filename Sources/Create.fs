namespace ReAction

open System.Threading
open System

open Types

module Creation =
    // Create async observable from async worker function
    let ofAsync (worker : AsyncObserver<'a> -> CancellationToken -> Async<unit>) : AsyncObservable<_> =
        let subscribe (aobv : AsyncObserver<_>) : Async<AsyncDisposable> =
            let cancel, token = Core.canceller ()
            let obv = Core.safeObserver aobv

            async {
                let! _ = Async.StartChild (worker obv token, 0)
                return cancel
            }
        subscribe

    // An async observervable that just completes when subscribed.
    let inline empty () : AsyncObservable<'a> =
        ofAsync (fun obv _ -> async {
            do! OnCompleted |> obv
        })

    // An async observervable that just fails with an error when subscribed.
    let inline fail (exn) : AsyncObservable<'a> =
        ofAsync (fun obv _ -> async {
            do! OnError exn |> obv
        })

    let ofSeq (xs : seq<'a>) : AsyncObservable<'a> =
        ofAsync (fun obv token -> async {
            for x in xs do
                if token.IsCancellationRequested then
                    raise <| OperationCanceledException("Operation cancelled")

                try
                    do! OnNext x |> obv
                with ex ->
                    do! OnError ex |> obv

            do! OnCompleted |> obv
        })

    let inline single (x : 'a) : AsyncObservable<'a> =
        ofSeq [ x ]

    // Create an async observable from a subscribe function. So trivial
    // we should remove it once we get used to the idea that subscribe is
    // exactly the same as an async observable.
    let create (subscribe : AsyncObserver<_> -> Async<AsyncDisposable>) : AsyncObservable<_> =
        subscribe
