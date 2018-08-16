// Keep in separate file as it messes up the VSC syntax highlighting
namespace ReAction

[<AutoOpen>]
module Overloads =
    ()
(*
    type AsyncObservablePlus = AsyncObservablePlus with
        // Concatenate two AsyncObservable streams
        static member (?<-) (AsyncObservablePlus, x:AsyncObservable<'a>, y:AsyncObservable<'a>) = concat [x; y]
        // Start AsyncObservable with given sequence
        static member (?<-) (AsyncObservablePlus, x:seq<'a>, y:AsyncObservable<'a>) = startWith x y
        // Make sure (+) still works for normal types
        static member inline (?<-) (AsyncObservablePlus, x, y) = x + y

    let inline (+) (v1:'a) (v2:'a) : 'a = (?<-) AsyncObservablePlus v1 v2

    type AsyncObservableBind = AsyncObservableBind with
        static member (?<-) (AsyncObservableBind, x:AsyncMapper<'a, AsyncObservable<'b>>, y:AsyncObservable<'a>) : AsyncObservable<'b> = flatMapAsync x y

        static member (?<-) (AsyncObservableBind, x:Mapper<'a, AsyncObservable<'b>>, y:AsyncObservable<'a>) : AsyncObservable<'b> = flatMap x y

        static member (?<-) (AsyncObservableBind, x:AsyncMapperIndexed<'a, AsyncObservable<'b>>, y:AsyncObservable<'a>) : AsyncObservable<'b> = flatMapIndexedAsync x y

        static member (?<-) (AsyncObservableBind, x:MapperIndexed<'a, AsyncObservable<'b>>, y:AsyncObservable<'a>) : AsyncObservable<'b> = flatMapIndexed x y

    let inline (>>=) v1 v2 = (?<-) AsyncObservableBind v1 v2
*)