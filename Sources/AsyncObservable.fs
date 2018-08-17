namespace ReAction

[<AutoOpen>]
module AsyncObservable =
    type AsyncObservable<'a> = AsyncObservable of Types.AsyncObservable<'a> with
        member this.Unwrap = match this with AsyncObservable obs -> obs

        member this.Subscribe obv = (fun (AsyncObservable obs) -> obs) this obv

        member this.Subscribe<'a> (obv: Notification<'a> -> Async<unit>) = (fun (AsyncObservable obs) -> obs) this <| obv

         // Concatenate two AsyncObservable streams
        static member (+) (x:AsyncObservable<'a>, y:AsyncObservable<'a>) =
            concat [x.Unwrap; y.Unwrap]

        static member (>>=) (mapper:Types.AsyncMapper<'a, AsyncObservable<'b>>, source:AsyncObservable<'a>) : AsyncObservable<'b> =
            let mapperUnwrapped a = async {
                let! result = mapper a
                return result.Unwrap
            }
            AsyncObservable <| (Transform.flatMapAsync mapperUnwrapped (source.Unwrap))

        static member (>>=) (mapper:Types.AsyncMapperIndexed<'a, AsyncObservable<'b>>, source:AsyncObservable<'a>) : AsyncObservable<'b> =
            let mapperUnwrapped a i = async {
                let! result = mapper a i
                return result.Unwrap
            }
            AsyncObservable <| (Transform.flatMapIndexedAsync mapperUnwrapped (source.Unwrap))

        static member (>>=) (mapper: Types.MapperIndexed<'a, AsyncObservable<'b>>, source:AsyncObservable<'a>) : AsyncObservable<'b> =
            let mapperUnwrapped a i =
                let result = mapper a i
                result.Unwrap

            AsyncObservable <| (Transform.flatMapIndexed mapperUnwrapped (source.Unwrap))

        member this.map (mapper : Types.AsyncMapper<'a,'b>) : AsyncObservable<'b> =
            AsyncObservable <| Transform.mapAsync mapper this.Unwrap

        member this.map (mapper : Types.Mapper<'a,'b>) : AsyncObservable<'b> =
            AsyncObservable <| Transform.map mapper this.Unwrap

    let from (xs : seq<'a>) : AsyncObservable<'a> =
        AsyncObservable <| Creation.from xs

    let empty () : AsyncObservable<'a> =
        AsyncObservable <| Creation.empty ()

    let fail ex : AsyncObservable<'a> =
        AsyncObservable <| Creation.fail ex

    let inline just (x : 'a) : AsyncObservable<'a> =
        from [ x ]

