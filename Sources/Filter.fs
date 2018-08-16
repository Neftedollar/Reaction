namespace ReAction

[<AutoOpen>]
module Filter =
    // The classic filter (where) operator with async predicate
    let filterAsync (predicate : AsyncPredicate<'a>) (source : AsyncObservable<'a>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            async {
                let obv n =
                    async {
                        match n with
                        | OnNext x ->
                            let! result = predicate x
                            if result then
                                do! aobv.OnNext x  // Let exceptions bubble to the top
                        | _ -> do! aobv.Call n
                    }
                return! source.Subscribe obv
            }
        AsyncObservable subscribe

    // The classic filter (where) operator with sync predicate
    let inline filter (predicate : Re.Predicate<'a>) (source : AsyncObservable<'a>) : AsyncObservable<'a> =
        filterAsync (fun x -> async { return predicate x }) source

    let distinctUntilChanged (source : AsyncObservable<'a>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            let safeObserver = safeObserver aobv
            let agent = MailboxProcessor.Start(fun inbox ->
                let rec messageLoop (latest : Notification<'a>) = async {
                    let! n = inbox.Receive()

                    let! latest' = async {
                        match n with
                        | OnNext x ->
                            if n <> latest then
                                try
                                    do! safeObserver.OnNext x
                                with
                                | ex -> do! safeObserver.OnError ex
                        | _ -> do! safeObserver.Call n
                        return n
                    }

                    return! messageLoop latest'
                }

                messageLoop OnCompleted // Use as sentinel value as it will not match any OnNext value
            )

            async {
                let obv n =
                    async {
                        agent.Post n
                    }
                return! source.Subscribe obv
            }
        AsyncObservable subscribe

