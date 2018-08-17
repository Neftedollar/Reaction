namespace ReAction

[<AutoOpen>]
module AsyncObserver =
    type AsyncDisposable = AsyncDisposable of Types.AsyncDisposable

    type AsyncObserver<'a> = AsyncObserver of Types.AsyncObserver<'a> with
        member this.Unwrap = match this with AsyncObserver obv -> obv

        member this.OnNext (x : 'a) = this.Unwrap <| OnNext x
        member this.OnError err = this.Unwrap <| OnError err
        member this.OnCompleted () = this.Unwrap <| OnCompleted

        member this.Call n = this.Unwrap <| n

