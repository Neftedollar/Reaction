namespace AsyncReactive

open System

module Types =
    type Notification<'a> =
        | OnNext of 'a
        | OnError of Exception
        | OnCompleted

    type AsyncDisposable = unit -> Async<unit>
    type AsyncObserver<'a> = Notification<'a> -> Async<unit>
    type AsyncObservable<'a> = AsyncObserver<'a> -> Async<AsyncDisposable>

    type AsyncMapper<'a, 'b> = 'a -> Async<'b>
    type AsyncPredicate<'a> = 'a -> Async<bool>
