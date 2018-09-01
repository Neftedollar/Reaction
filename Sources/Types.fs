namespace Reaction

type Notification<'a> =
    | OnNext of 'a
    | OnError of exn
    | OnCompleted

module Types =
    type AsyncDisposable = unit -> Async<unit>
    type AsyncObserver<'a> = Notification<'a> -> Async<unit>
    type AsyncObservable<'a> = AsyncObserver<'a> -> Async<AsyncDisposable>

    type Accumulator<'s, 't> = 's -> 't -> 's

    type RefCountCmd =
        | Increase
        | Decrease

    type InnerSubscriptionCmd<'a> =
        | InnerObservable of AsyncObservable<'a>
        | Dispose


    type MailboxProcessor<'Msg> with
        static member StartImmediate(body, ?cancellationToken) =
            let mb = new MailboxProcessor<'Msg>(body,?cancellationToken=cancellationToken)

            // Protect the execution and send errors to the event.
            // Note that exception stack traces are lost in this design - in an extended design
            // the event could propagate an ExceptionDispatchInfo instead of an Exception.
            let p = async {
                try
                    do! body mb
                with exn ->
                    printfn "Got exception: %A" exn
                }

            let token = defaultArg cancellationToken Async.DefaultCancellationToken

            Async.StartImmediate(computation=p, cancellationToken=token)
            mb


