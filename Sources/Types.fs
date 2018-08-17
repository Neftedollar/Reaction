namespace ReAction

type Notification<'a> =
    | OnNext of 'a
    | OnError of exn
    | OnCompleted

module Types =
    type AsyncDisposable = unit -> Async<unit>
    type AsyncObserver<'a> = Notification<'a> -> Async<unit>
    type AsyncObservable<'a> = AsyncObserver<'a> -> Async<AsyncDisposable>

    type AsyncMapper<'a, 'b> = 'a -> Async<'b>
    type AsyncPredicate<'a> = 'a -> Async<bool>
    type AsyncAccumulator<'s, 't> = 's -> 't -> Async<'s>
    type AsyncMapperIndexed<'a, 'b> = 'a -> int -> Async<'b>

    type Mapper<'a, 'b> = 'a -> 'b
    type MapperIndexed<'a, 'b> = 'a -> int -> 'b
    type Accumulator<'s, 't> = 's -> 't -> 's

    type RefCountCmd =
        | Increase
        | Decrease

    type InnerSubscriptionCmd<'a> =
        | InnerObservable of AsyncObservable<'a>
        | Dispose

// To avoid conflict with System.Predicate
module Re =
    type Predicate<'a> = 'a -> bool

