// Keep in separate file as it messes up the VSC syntax highlighting
namespace ReAction

[<AutoOpen>]
module Overloads =
    type AsyncDisposable with
        member this.Dispose () = (fun (AsyncDisposable d) -> d) this ()

    type AsyncObserver<'a> with
        static member Unwrap (AsyncObserver obv) = obv
        member this.OnNext x = AsyncObserver.Unwrap this <| OnNext x
        member this.OnError err = AsyncObserver.Unwrap this <| OnError err
        member this.OnCompleted () = AsyncObserver.Unwrap this <| OnCompleted

        member this.Call n = AsyncObserver.Unwrap this <| n

    type AsyncObservable<'a> with
        member this.Subscribe obv = (fun (AsyncObservable obs) -> obs) this obv

        member this.Subscribe<'a> (obv: Notification<'a> -> Async<unit>) = (fun (AsyncObservable obs) -> obs) this <| AsyncObserver obv

        // Concatenate two AsyncObservable streams
        static member (+) (x:AsyncObservable<'a>, y:AsyncObservable<'a>) = concat [x; y]
        // Start AsyncObservable with given sequence
        static member (+) (x:seq<'a>, y:AsyncObservable<'a>) = startWith x y

        static member ( >>= ) (x:AsyncMapper<'a, AsyncObservable<'b>>, y:AsyncObservable<'a>) : AsyncObservable<'b> = flatMapAsync x y

        static member (>>=) (x:Mapper<'a, AsyncObservable<'b>>, y:AsyncObservable<'a>) : AsyncObservable<'b> = flatMap x y

        static member (>>=) (x:AsyncMapperIndexed<'a, AsyncObservable<'b>>, y:AsyncObservable<'a>) : AsyncObservable<'b> = flatMapIndexedAsync x y

        static member (>>=) (x:MapperIndexed<'a, AsyncObservable<'b>>, y:AsyncObservable<'a>) : AsyncObservable<'b> = flatMapIndexed x y

