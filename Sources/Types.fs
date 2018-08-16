namespace ReAction

type Notification<'a> =
    | OnNext of 'a
    | OnError of exn
    | OnCompleted

type AsyncMapper<'a, 'b> = 'a -> Async<'b>
type AsyncPredicate<'a> = 'a -> Async<bool>
type AsyncAccumulator<'s, 't> = 's -> 't -> Async<'s>
type AsyncMapperIndexed<'a, 'b> = 'a -> int -> Async<'b>

type Mapper<'a, 'b> = 'a -> 'b
type MapperIndexed<'a, 'b> = 'a -> int -> 'b
type Accumulator<'s, 't> = 's -> 't -> 's

type AsyncDisposable = AsyncDisposable of (unit -> Async<unit>)
type AsyncObserver<'a> = AsyncObserver of (Notification<'a> -> Async<unit>)
type AsyncObservable<'a> = AsyncObservable of (AsyncObserver<'a> -> Async<AsyncDisposable>) with
    // Concatenate two AsyncObservable streams
    static member (+) (x:AsyncObservable<'a>, y:AsyncObservable<'a>) = concat [x; y]
    // Start AsyncObservable with given sequence
    static member (+) (x:seq<'a>, y:AsyncObservable<'a>) = startWith x y

    static member (>>=) (x:AsyncMapper<'a, AsyncObservable<'b>>, y:AsyncObservable<'a>) : AsyncObservable<'b> = flatMapAsync x y

    static member (>>=) (x:Mapper<'a, AsyncObservable<'b>>, y:AsyncObservable<'a>) : AsyncObservable<'b> = flatMap x y

    static member (>>=) (x:AsyncMapperIndexed<'a, AsyncObservable<'b>>, y:AsyncObservable<'a>) : AsyncObservable<'b> = flatMapIndexedAsync x y

    static member (>>=) (x:MapperIndexed<'a, AsyncObservable<'b>>, y:AsyncObservable<'a>) : AsyncObservable<'b> = flatMapIndexed x y


type RefCountCmd =
    | Increase
    | Decrease

type InnerSubscriptionCmd<'a> =
    | InnerObservable of AsyncObservable<'a>
    | Dispose

// To avoid conflict with System.Predicate
module Re =
    type Predicate<'a> = 'a -> bool

