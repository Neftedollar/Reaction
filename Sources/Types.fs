namespace AsyncReactive

module Types =
    type Notification<'a> =
        | OnNext of 'a
        | OnError of exn
        | OnCompleted

    type AsyncDisposable = unit -> Async<unit>
    type AsyncObserver<'a> = Notification<'a> -> Async<unit>
    type AsyncObservable<'a> = AsyncObserver<'a> -> Async<AsyncDisposable>

    type AsyncMapper<'a, 'b> = 'a -> Async<'b>
    type AsyncPredicate<'a> = 'a -> Async<bool>
    type AsyncAccumulator<'s, 't> = 's -> 't -> Async<'s>
