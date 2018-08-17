namespace ReAction

type Notification<'a> =
    | OnNext of 'a
    | OnError of exn
    | OnCompleted

module Types =
    type AsyncDisposable = unit -> Async<unit>
    type AsyncObserver<'a> = Notification<'a> -> Async<unit>
    type AsyncObservable<'a> = AsyncObserver<'a> -> Async<AsyncDisposable>

    type RefCountCmd =
        | Increase
        | Decrease

    type InnerSubscriptionCmd<'a> =
        | InnerObservable of AsyncObservable<'a>
        | Dispose

