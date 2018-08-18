namespace ReAction

[<AutoOpen>]
module AsyncObserver =
    type AsyncDisposable = AsyncDisposable of Types.AsyncDisposable

    type AsyncObserver<'a> = AsyncObserver of Types.AsyncObserver<'a> with
        static member Unwrap (AsyncObserver obv) : Types.AsyncObserver<'a> = obv

        member this.OnNextAsync (x : 'a) = AsyncObserver.Unwrap this <| OnNext x
        member this.OnErrorAsync err = AsyncObserver.Unwrap this <| OnError err
        member this.OnCompletedAsync () = AsyncObserver.Unwrap this <| OnCompleted

        member this.NotifyAsync n = AsyncObserver.Unwrap this n

