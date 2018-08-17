namespace ReAction

[<AutoOpen>]
module AsyncObservable =
    type AsyncObservable<'a> = AsyncObservable of Types.AsyncObservable<'a> with

        static member unwrap (AsyncObservable obs) : Types.AsyncObservable<'a> = obs

        member this.Subscribe obv = (fun (AsyncObservable obs) -> obs) this obv

        member this.Subscribe<'a> (obv: Notification<'a> -> Async<unit>) = (fun (AsyncObservable obs) -> obs) this <| obv

         // Concatenate two AsyncObservable streams
        static member (+) (x:AsyncObservable<'a>, y:AsyncObservable<'a>) =
            Combine.concat [ AsyncObservable.unwrap x; AsyncObservable.unwrap y]

        static member (>>=) (source:AsyncObservable<'a>, mapper:'a*int -> Async<AsyncObservable<'b>>) : AsyncObservable<'b> =
            let mapperUnwrapped p : Async<Types.AsyncObservable<'b>> = async {
                let! result = mapper p
                return AsyncObservable.unwrap result
            }
            AsyncObservable.unwrap source |> Transform.flatMapIndexedAsync mapperUnwrapped |> AsyncObservable

    let from (xs : seq<'a>) : AsyncObservable<'a> =
        AsyncObservable <| Creation.from xs

    let empty () : AsyncObservable<'a> =
        AsyncObservable <| Creation.empty ()

    let fail ex : AsyncObservable<'a> =
        AsyncObservable <| Creation.fail ex

    let just (x : 'a) : AsyncObservable<'a> =
        from [ x ]

    let delay msecs (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.unwrap source |>  Timeshift.delay msecs |> AsyncObservable

    let debounce msecs (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.unwrap source |>  Timeshift.debounce msecs |> AsyncObservable

    let map (mapper:'a*int -> Async<'b>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.unwrap source |> Transform.mapIndexedAsync mapper |> AsyncObservable

    let inline merge (source : AsyncObservable<AsyncObservable<'a>>) : AsyncObservable<'a> =
        AsyncObservable.unwrap source
            |> Transform.map (fun xs -> AsyncObservable.unwrap xs)
            |> Combine.merge
            |> AsyncObservable

    let inline concat (sources : seq<AsyncObservable<'a>>) : AsyncObservable<'a> =
        Seq.map AsyncObservable.unwrap sources
            |> Combine.concat
            |> AsyncObservable

    let flatMap (mapper:'a*int -> Async<AsyncObservable<'b>>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        let mapperUnwrapped p : Async<Types.AsyncObservable<'b>> = async {
            let! result = mapper p
            return AsyncObservable.unwrap result
        }
        AsyncObservable.unwrap source
            |> Transform.flatMapIndexedAsync mapperUnwrapped
            |> AsyncObservable

    let filter (predicate: 'a -> Async<bool>) (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.unwrap source
            |> Filter.filterAsync predicate
            |> AsyncObservable

    let scan (initial : 's) (scanner:'s -> 'a -> Async<'s>) (source: AsyncObservable<'a>) : AsyncObservable<'s> =
        AsyncObservable.unwrap source
            |> Aggregate.scanAsync initial scanner
            |> AsyncObservable

    let stream<'a> () : AsyncObserver<'a> * AsyncObservable<'a> =
        let obv, obs =Streams.stream ()
        AsyncObserver obv, AsyncObservable obs