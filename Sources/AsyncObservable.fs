namespace Reaction

[<AutoOpen>]
module AsyncObservable =
    /// AsyncObservable as a single case union type to attach methods such as SubscribeAsync.
    type AsyncObservable<'a> = AsyncObservable of Types.AsyncObservable<'a> with

        /// Returns the wrapped subscribe function: AsyncObserver{'a} -> Async{AsyncDisposable}
        static member Unwrap (AsyncObservable obs) : Types.AsyncObservable<'a> = obs

        /// Subscribes the async observer to the async observable
        member this.SubscribeAsync obv = async {
            let! disposable = AsyncObserver.Unwrap obv |> AsyncObservable.Unwrap this
            return AsyncDisposable disposable
        }

        /// Subscribes the async observer to the async observable,
        /// ignores the disposable
        member this.RunAsync obv = async {
            let! _ = AsyncObserver.Unwrap obv |> AsyncObservable.Unwrap this
            return ()
        }

        /// Subscribes the observer function (Notification{'a} -> Async{unit}) to the AsyncObservable
        member this.SubscribeAsync<'a> (obv: Notification<'a> -> Async<unit>) = async{
            let! disposable = obv |> AsyncObservable.Unwrap this
            return AsyncDisposable disposable
        }

        /// Subscribes the observer function (Notification{'a} -> Async{unit}) to the AsyncObservable, ignores the disposable
        member this.RunAsync<'a> (obv: Notification<'a> -> Async<unit>) = async {
            do! obv |> AsyncObservable.Unwrap this |> Async.Ignore
        }

        /// Returns an observable sequence that contains the elements of
        /// the two steams, in sequential order.
        static member (+) (x: AsyncObservable<'a>, y: AsyncObservable<'a>) =
            Combine.concat [ AsyncObservable.Unwrap x; AsyncObservable.Unwrap y]
            |> AsyncObservable

        /// Projects each element of an observable sequence into an
        /// observable sequence and merges the resulting observable
        /// sequences back into one observable sequence.
        static member (>>=) (source:AsyncObservable<'a>, mapper:'a -> Async<AsyncObservable<'b>>) : AsyncObservable<'b> =
            let mapperUnwrapped p : Async<Types.AsyncObservable<'b>> = async {
                let! result = mapper p
                return AsyncObservable.Unwrap result
            }
            AsyncObservable.Unwrap source
            |> Transform.flatMapAsync mapperUnwrapped
            |> AsyncObservable

    let mapperUnwrapped (mapper : 'a -> AsyncObservable<'b>) a : Types.AsyncObservable<'b> =
            let result = mapper a
            AsyncObservable.Unwrap result

    let mapperUnwrappedAsync mapper a : Async<Types.AsyncObservable<'b>> = async {
        let! result = mapper a
        return AsyncObservable.Unwrap result
    }

    /// Returns the observable sequence whose elements are pulled from
    /// the given enumerable sequence.
    let ofSeq (xs : seq<'a>) : AsyncObservable<'a> =
        AsyncObservable <| Creation.ofSeq xs

    /// Returns an observable sequence with no elements.
    let empty<'a> () : AsyncObservable<'a> =
        AsyncObservable <| Creation.empty ()

    /// Returns the observable sequence that terminates exceptionally
    /// with the specified exception.
    let fail<'a> ex : AsyncObservable<'a> =
        AsyncObservable <| Creation.fail ex

    /// Returns an observable sequence containing the single specified
    /// element.
    let single (x : 'a) : AsyncObservable<'a> =
        AsyncObservable <| Creation.single x

    /// Time shifts the observable sequence by the given timeotu. The
    /// relative time intervals between the values are preserved.
    let delay msecs (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.Unwrap source |>  Timeshift.delay msecs |> AsyncObservable

    /// Ignores values from an observable sequence which are followed by
    /// another value before the given timeout.
    let debounce msecs (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.Unwrap source |>  Timeshift.debounce msecs |> AsyncObservable

    /// Returns an observable sequence whose elements are the result of
    /// invoking the mapper function on each element of the source.
    let map (mapper:'a -> 'b) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.Unwrap source |> Transform.map mapper |> AsyncObservable

    /// Returns an observable sequence whose elements are the result of
    /// invoking the async mapper function on each element of the source.
    let mapAsync (mapper:'a -> Async<'b>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.Unwrap source |> Transform.mapAsync mapper |> AsyncObservable

    /// Returns an observable sequence whose elements are the result of
    /// invoking the mapper function and incorporating the element's
    /// index on each element of the source.
    let mapi (mapper:'a*int -> 'b) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.Unwrap source |> Transform.mapi mapper |> AsyncObservable

    /// Returns an observable sequence whose elements are the result of
    /// invoking the async mapper function by incorporating the element's
    /// index on each element of the source.
    let mapiAsync (mapper:'a*int -> Async<'b>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.Unwrap source |> Transform.mapiAsync mapper |> AsyncObservable

    /// Merges an observable sequence of observable sequences into an
    /// observable sequence.
    let inline merge (source : AsyncObservable<AsyncObservable<'a>>) : AsyncObservable<'a> =
        AsyncObservable.Unwrap source
        |> Transform.map AsyncObservable.Unwrap
        |> Combine.merge
        |> AsyncObservable

    /// Returns an observable sequence that contains the elements of each given
    /// sequences, in sequential order.
    let inline concat (sources : seq<AsyncObservable<'a>>) : AsyncObservable<'a> =
        Seq.map AsyncObservable.Unwrap sources
        |> Combine.concat
        |> AsyncObservable

    /// Projects each element of an observable sequence into an
    /// observable sequence and merges the resulting observable
    /// sequences back into one observable sequence.
    let flatMap (mapper:'a -> AsyncObservable<'b>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.Unwrap source
        |> Transform.flatMap (mapperUnwrapped mapper)
        |> AsyncObservable

    /// Projects each element of an observable sequence into an
    /// observable sequence by incorporating the element's
    /// index on each element of the source. Merges the resulting
    /// observable sequences back into one observable sequence.
    let flatMapi (mapper:'a*int -> AsyncObservable<'b>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.Unwrap source
        |> Transform.flatMapi (mapperUnwrapped mapper)
        |> AsyncObservable

    /// Asynchronously projects each element of an observable sequence
    /// into an observable sequence and merges the resulting observable
    /// sequences back into one observable sequence.
    let flatMapAsync (mapper:'a -> Async<AsyncObservable<'b>>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.Unwrap source
        |> Transform.flatMapAsync (mapperUnwrappedAsync mapper)
        |> AsyncObservable

    /// Asynchronously projects each element of an observable sequence
    /// into an observable sequence by incorporating the element's
    /// index on each element of the source. Merges the resulting
    /// observable sequences back into one observable sequence.
    let flatMapiAsync (mapper:'a*int -> Async<AsyncObservable<'b>>) (source: AsyncObservable<'a>) : AsyncObservable<'b> =
        AsyncObservable.Unwrap source
        |> Transform.flatMapiAsync (mapperUnwrappedAsync mapper)
        |> AsyncObservable

    /// Filters the elements of an observable sequence based on a
    /// predicate. Returns an observable sequence that contains elements
    /// from the input sequence that satisfy the condition.
    let filter (predicate: 'a -> bool) (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.Unwrap source
        |> Filter.filter predicate
        |> AsyncObservable

    /// Filters the elements of an observable sequence based on an async
    /// predicate. Returns an observable sequence that contains elements
    /// from the input sequence that satisfy the condition.
    let filterAsync (predicate: 'a -> Async<bool>) (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.Unwrap source
        |> Filter.filterAsync predicate
        |> AsyncObservable

    /// Return an observable sequence only containing the distinct
    /// contiguous elementsfrom the source sequence.
    let distinctUntilChanged (source : AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.Unwrap source
        |> Filter.distinctUntilChanged
        |> AsyncObservable

    /// Applies an accumulator function over an observable sequence and
    /// returns each intermediate result. The seed value is used as the
    /// initial accumulator value. Returns an observable sequence
    /// containing the accumulated values.
    let scan (initial : 's) (scanner:'s -> 'a -> 's) (source: AsyncObservable<'a>) : AsyncObservable<'s> =
        AsyncObservable.Unwrap source
        |> Aggregate.scan initial scanner
        |> AsyncObservable

    /// Applies an async accumulator function over an observable
    /// sequence and returns each intermediate result. The seed value is
    /// used as the initial accumulator value. Returns an observable
    /// sequence containing the accumulated values.
    let scanAsync (initial : 's) (scanner:'s -> 'a -> Async<'s>) (source: AsyncObservable<'a>) : AsyncObservable<'s> =
        AsyncObservable.Unwrap source
        |> Aggregate.scanAsync initial scanner
        |> AsyncObservable

    /// A stream is both an observable sequence as well as an observer.
    /// Each notification is broadcasted to all subscribed observers.
    let stream<'a> () : AsyncObserver<'a> * AsyncObservable<'a> =
        let obv, obs = Streams.stream ()
        AsyncObserver obv, AsyncObservable obs

    /// Returns an observable sequence containing the first sequence's
    /// elements, followed by the elements of the handler sequence in
    /// case an exception occurred.
    let inline catch (handler: exn -> AsyncObservable<'a>) (source: AsyncObservable<'a>) : AsyncObservable<'a> =
        AsyncObservable.Unwrap source
        |> Transform.catch (mapperUnwrapped handler)
        |> AsyncObservable

    /// Prepends a sequence of values to an observable sequence.
    /// Returns the source sequence prepended with the specified values.
    let inline startWith (items : seq<'a>) (source : AsyncObservable<'a>) =
        AsyncObservable.Unwrap source
        |> Combine.startWith items
        |> AsyncObservable

    /// Merges the specified observable sequences into one observable
    /// sequence by combining elements of the sources into tuples.
    /// Returns an observable sequence containing the combined results.
    let inline combineLatest (other : AsyncObservable<'b>) (source : AsyncObservable<'a>) : AsyncObservable<'a*'b> =
        AsyncObservable.Unwrap source
        |> Combine.combineLatest (AsyncObservable.Unwrap other)
        |> AsyncObservable

    /// Merges the specified observable sequences into one observable
    /// sequence by combining the values into tuples only when the first
    /// observable sequence produces an element. Returns the combined
    /// observable sequence.
    let inline withLatestFrom (other : AsyncObservable<'b>) (source : AsyncObservable<'a>) : AsyncObservable<'a*'b> =
        AsyncObservable.Unwrap source
        |> Combine.withLatestFrom (AsyncObservable.Unwrap other)
        |> AsyncObservable

    /// Groups the elements of an observable sequence according to a
    /// specified key mapper function. Returns a sequence of observable
    /// groups, each of which corresponds to a given key.
    let groupBy (keyMapper: 'a -> 'g) (source : AsyncObservable<'a>) : AsyncObservable<AsyncObservable<'a>> =
        AsyncObservable.Unwrap source
        |> Aggregate.groupBy keyMapper
        |> Transform.map AsyncObservable
        |> AsyncObservable
