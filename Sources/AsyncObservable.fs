namespace ReAction

type TMapper<'a, 'b> =
    // abstract method
    | Mapper of ('a -> 'b)
    | MapperAsync of ('a -> Async<'b>)

[<AutoOpen>]
module AsyncObservable =
    type AsyncObservable<'a> = AsyncObservable of Types.AsyncObservable<'a> with
        member this.Unwrap = match this with AsyncObservable obs -> obs

        member this.Subscribe obv = (fun (AsyncObservable obs) -> obs) this obv

        member this.Subscribe<'a> (obv: Notification<'a> -> Async<unit>) = (fun (AsyncObservable obs) -> obs) this <| obv

         // Concatenate two AsyncObservable streams
        static member (+) (x:AsyncObservable<'a>, y:AsyncObservable<'a>) =
            concat [x.Unwrap; y.Unwrap]

        static member (>>=) (source:AsyncObservable<'a>, mapper:'a*int -> Async<AsyncObservable<'b>>) : AsyncObservable<'b> =
            let mapperUnwrapped p : Async<Types.AsyncObservable<'b>> = async {
                let! result = mapper p
                return result.Unwrap
            }
            AsyncObservable <| (Transform.flatMapIndexedAsync mapperUnwrapped source.Unwrap)

    let from (xs : seq<'a>) : AsyncObservable<'a> =
        AsyncObservable <| Creation.from xs

    let empty () : AsyncObservable<'a> =
        AsyncObservable <| Creation.empty ()

    let fail ex : AsyncObservable<'a> =
        AsyncObservable <| Creation.fail ex

    let just (x : 'a) : AsyncObservable<'a> =
        from [ x ]

    let delay msecs (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable <| Timeshift.debounce msecs source.Unwrap

    //let inline merge (source : AsyncObservable<AsyncObservable<'a>>) : AsyncObservable<'a> =
    //    AsyncObservable <| Combine.merge source.Unwrap

    let map (mapper:'a*int -> Async<'b>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable <| Transform.mapIndexedAsync mapper source.Unwrap

    let filter predicate (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        match box predicate with
        | :? ('a -> bool) as p -> AsyncObservable <| Filter.filter p source.Unwrap
        | :? ('a -> Async<bool>) as p -> AsyncObservable <| Filter.filterAsync p source.Unwrap
        | _ -> empty ()

    let scan (initial : 's) (scanner:'s -> 'a -> Async<'s>) (source: AsyncObservable<'a>) : AsyncObservable<'s> =
        AsyncObservable <| Aggregate.scanAsync initial scanner source.Unwrap
