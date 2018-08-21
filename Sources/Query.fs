module ReAction.Query

open ReAction

type QueryBuilder() =
    member this.Zero() = empty ()

    member this.YieldFrom (x) : AsyncObservable<_> = x

    member this.Yield (x : 'a) : AsyncObservable<_> = single x

    member this.Bind(source: AsyncObservable<'a>, fn: 'a -> AsyncObservable<'b>) = flatMap fn source

// Query builder for an async reactive event source
let reaction = new QueryBuilder()
