namespace ReAction

[<AutoOpen>]
module AsyncObservable =
    type AsyncObservable<'a> = AsyncObservable of Types.AsyncObservable<'a> with

        /// Returns the wrapped subscribe function: AsyncObserver{'a} -> Async{AsyncDisposable}
        static member Unwrap (AsyncObservable obs) : Types.AsyncObservable<'a> = obs

        /// Subscribes an AsyncObserver to the AsyncObservable
        member this.SubscribeAsync obv = async {
            let! disposable = AsyncObserver.Unwrap obv |> AsyncObservable.Unwrap this
            return AsyncDisposable disposable
        }

        /// Subscribes an AsyncObserver to the AsyncObservable, ignores the disposable
        member this.SubscribeAsyncIgnore obv = async {
            let! _ = AsyncObserver.Unwrap obv |> AsyncObservable.Unwrap this
            return ()
        }

        /// Subscribes the observer function (Notification{'a} -> Async{unit}) to the AsyncObservable
        member this.SubscribeAsync<'a> (obv: Notification<'a> -> Async<unit>) = async{
            let! disposable = obv |> AsyncObservable.Unwrap this
            return AsyncDisposable disposable
        }

        /// Subscribes the observer function (Notification{'a} -> Async{unit}) to the AsyncObservable, ignores the disposable
        member this.SubscribeAsyncIgnore<'a> (obv: Notification<'a> -> Async<unit>) = async {
            let! _ = obv |> AsyncObservable.Unwrap this
            ()
        }

         // Concatenate two AsyncObservable streams
        static member (+) (x:AsyncObservable<'a>, y:AsyncObservable<'a>) =
            Combine.concat [ AsyncObservable.Unwrap x; AsyncObservable.Unwrap y]

        // FlapMapAsync overload. Note cannot be used by Fable
        static member (>>=) (source:AsyncObservable<'a>, mapper:'a*int -> Async<AsyncObservable<'b>>) : AsyncObservable<'b> =
            let mapperUnwrapped p : Async<Types.AsyncObservable<'b>> = async {
                let! result = mapper p
                return AsyncObservable.Unwrap result
            }
            AsyncObservable.Unwrap source |> Transform.flatMapiAsync mapperUnwrapped |> AsyncObservable

        // FlapMapiAsync overload. Note cannot be used by Fable
        static member (>>=) (source:AsyncObservable<'a>, mapper:'a -> Async<AsyncObservable<'b>>) : AsyncObservable<'b> =
            let mapperUnwrapped p : Async<Types.AsyncObservable<'b>> = async {
                let! result = mapper p
                return AsyncObservable.Unwrap result
            }
            AsyncObservable.Unwrap source |> Transform.flatMapAsync mapperUnwrapped |> AsyncObservable

    let from (xs : seq<'a>) : AsyncObservable<'a> =
        AsyncObservable <| Creation.from xs

    let empty () : AsyncObservable<'a> =
        AsyncObservable <| Creation.empty ()

    let fail ex : AsyncObservable<'a> =
        AsyncObservable <| Creation.fail ex

    let just (x : 'a) : AsyncObservable<'a> =
        AsyncObservable <| Creation.just x

    let delay msecs (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.Unwrap source |>  Timeshift.delay msecs |> AsyncObservable

    let debounce msecs (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.Unwrap source |>  Timeshift.debounce msecs |> AsyncObservable

    let map (mapper:'a -> 'b) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.Unwrap source |> Transform.map mapper |> AsyncObservable

    let mapAsync (mapper:'a -> Async<'b>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.Unwrap source |> Transform.mapAsync mapper |> AsyncObservable

    let mapi (mapper:'a*int -> 'b) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.Unwrap source |> Transform.mapi mapper |> AsyncObservable

    let mapiAsync (mapper:'a*int -> Async<'b>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.Unwrap source |> Transform.mapiAsync mapper |> AsyncObservable

    let inline merge (source : AsyncObservable<AsyncObservable<'a>>) : AsyncObservable<'a> =
        AsyncObservable.Unwrap source
            |> Transform.map (fun xs -> AsyncObservable.Unwrap xs)
            |> Combine.merge
            |> AsyncObservable

    let inline concat (sources : seq<AsyncObservable<'a>>) : AsyncObservable<'a> =
        Seq.map AsyncObservable.Unwrap sources
            |> Combine.concat
            |> AsyncObservable

    let flatMapi (mapper:'a*int -> AsyncObservable<'b>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        let mapperUnwrapped p : Types.AsyncObservable<'b> =
            let result = mapper p
            AsyncObservable.Unwrap result

        AsyncObservable.Unwrap source
            |> Transform.flatMapi mapperUnwrapped
            |> AsyncObservable

    let flatMapiAsync (mapper:'a*int -> Async<AsyncObservable<'b>>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        let mapperUnwrapped p : Async<Types.AsyncObservable<'b>> = async {
            let! result = mapper p
            return AsyncObservable.Unwrap result
        }
        AsyncObservable.Unwrap source
            |> Transform.flatMapiAsync mapperUnwrapped
            |> AsyncObservable

    let filter (predicate: 'a -> Async<bool>) (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.Unwrap source
            |> Filter.filterAsync predicate
            |> AsyncObservable

    let distinctUntilChanged (source : AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.Unwrap source
            |> Filter.distinctUntilChanged
            |> AsyncObservable

    let scan (initial : 's) (scanner:'s -> 'a -> 's) (source: AsyncObservable<'a>) : AsyncObservable<'s> =
        AsyncObservable.Unwrap source
            |> Aggregate.scan initial scanner
            |> AsyncObservable

    let scanAsync (initial : 's) (scanner:'s -> 'a -> Async<'s>) (source: AsyncObservable<'a>) : AsyncObservable<'s> =
        AsyncObservable.Unwrap source
            |> Aggregate.scanAsync initial scanner
            |> AsyncObservable

    let stream<'a> () : AsyncObserver<'a> * AsyncObservable<'a> =
        let obv, obs = Streams.stream ()
        AsyncObserver obv, AsyncObservable obs