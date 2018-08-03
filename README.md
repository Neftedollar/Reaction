# FasyncReactive

Async Reactive (Rx) for F#. Currenty a playground project for experimenting with functional programming and async reactive (Async Observables) in F# using simple functions instead of classes and the traditional Rx interfaces.

```f#
type Notification<'a> =
    | OnNext of 'a
    | OnError of Exception
    | OnCompleted

type AsyncDisposable = unit -> Async<unit>
type AsyncObserver<'a> = Notification<'a> -> Async<unit>
type AsyncObservable<'a> = AsyncObserver<'a> -> Async<AsyncDisposable>

type AsyncMapper<'a, 'b> = 'a -> Async<'b>
type AsyncPredicate<'a> = 'a -> Async<bool>
```