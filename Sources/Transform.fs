namespace ReAction

[<AutoOpen>]
module Transform =
    // The classic map (select) operator with async mapper
    let mapAsync (mapper : AsyncMapper<'a,'b>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        let subscribe (aobv : AsyncObserver<'b>) =
            async {
                let obv' n =
                    async {
                        match n with
                        | OnNext x ->
                            let! b =  mapper x
                            do! aobv.OnNext b  // Let exceptions bubble to the top
                        | OnError ex -> do! aobv.OnError ex
                        | OnCompleted -> do! aobv.OnCompleted ()

                    }
                return! source.Subscribe obv'
            }
        AsyncObservable subscribe

    // The classic map (select) operator with sync mapper
    let inline map (mapper : Mapper<'a, 'b>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        mapAsync (fun x -> async { return mapper x }) source

    // The classic map (select) operator with an indexed and async mapper
    let mapIndexedAsync (mapper : AsyncMapperIndexed<'a, 'b>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        let infinite = Seq.initInfinite (fun index -> index)
        let indexer = infinite.GetEnumerator ()

        mapAsync (fun x -> async {
                    indexer.MoveNext () |> ignore
                    let index = indexer.Current
                    return! mapper x index
                  }) source

    // The classic map (select) operator with sync and indexed mapper
    let inline mapIndexed (mapper : MapperIndexed<'a, 'b>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        mapIndexedAsync (fun x i -> async { return mapper x i }) source

    // The classic flap map (selectMany, bind, mapMerge) operator
    let flatMap (mapper : Mapper<'a, AsyncObservable<'b>>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        source |> map mapper |> merge

    let flatMapIndexed (mapper : MapperIndexed<'a, AsyncObservable<'b>>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        source |> mapIndexed mapper |> merge

    let flatMapAsync (mapper : AsyncMapper<'a, AsyncObservable<'b>>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        source |> mapAsync mapper |> merge

    let flatMapIndexedAsync (mapper : AsyncMapperIndexed<'a, AsyncObservable<'b>>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        source |> mapIndexedAsync mapper |> merge

    let switchLatest (source : AsyncObservable<AsyncObservable<'a>>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            let safeObserver = safeObserver aobv
            let refCount = refCountActor 1 (async {
                do! safeObserver.OnCompleted ()
            })

            let innerActor =
                let obv n =
                    async {
                        match n with
                        | OnCompleted -> refCount.Post Decrease
                        | _ -> do! safeObserver.Call n
                    }

                MailboxProcessor.Start(fun inbox ->
                    let rec messageLoop (current : AsyncDisposable) = async {
                        let! cmd = inbox.Receive()
                        let getCurrent = async {
                            match cmd with
                            | InnerObservable xs ->
                                let! inner = xs.Subscribe obv
                                return inner
                            | Dispose ->
                                do! current.Dispose ()
                                return disposableEmpty
                        }
                        let! current' = getCurrent
                        return! messageLoop current'
                    }

                    messageLoop disposableEmpty
                )

            async {
                let obv (ns : Notification<AsyncObservable<'a>>) =
                    async {
                        match ns with
                        | OnNext xs ->
                            refCount.Post Increase
                            InnerObservable xs |> innerActor.Post
                        | OnError e -> do! safeObserver.OnError e
                        | OnCompleted -> refCount.Post Decrease
                    }

                let! dispose = source.Subscribe obv
                let cancel () =
                    async {
                        do! dispose.Dispose ()
                        innerActor.Post Dispose
                    }
                return AsyncDisposable cancel
            }
        AsyncObservable subscribe

    let flatMapLatest (mapper : Mapper<'a, AsyncObservable<'b>>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        source |> map mapper |> switchLatest

    let flatMapLatestAsync (mapper : AsyncMapper<'a, AsyncObservable<'b>>) (source : AsyncObservable<'a>) : AsyncObservable<'b> =
        source |> mapAsync mapper |> switchLatest