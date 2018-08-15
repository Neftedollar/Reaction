// Keep in separate file as it messes up the VSC syntax highlighting
namespace ReAction

[<AutoOpen>]
module Overloads =
    type AsyncObservablePlus = AsyncObservablePlus with
        // Concatenate two AsyncObservable streams
        static member (?<-) (AsyncObservablePlus, x:AsyncObservable<'a>, y:AsyncObservable<'a>) = concat [x; y]
        // Start AsyncObservable with given sequence
        static member (?<-) (AsyncObservablePlus, x:seq<'a>, y:AsyncObservable<'a>) = startWith x y
        // Make sure (+) still works for normal types
        static member (?<-) (AsyncObservablePlus, x, y) = x + y

    let inline (+) v1 v2 = (?<-) AsyncObservablePlus v1 v2
