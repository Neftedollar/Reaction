namespace ReAction

[<AutoOpen>]
module AsyncObserver =
    type AsyncDisposable = AsyncDisposable of Types.AsyncDisposable

    type AsyncObserver<'a> = AsyncObserver of Types.AsyncObserver<'a> with
        static member unwrap (AsyncObserver obv) : Types.AsyncObserver<'a> = obv

        member this.OnNext (x : 'a) = AsyncObserver.unwrap this <| OnNext x
        member this.OnError err = AsyncObserver.unwrap this <| OnError err
        member this.OnCompleted () = AsyncObserver.unwrap this <| OnCompleted

        member this.Call n = AsyncObserver.unwrap this <| n

