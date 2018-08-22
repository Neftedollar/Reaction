module Reaction.Query

open Reaction

type QueryBuilder() =
    member this.Zero() = empty ()

    member this.YieldFrom (x) : AsyncObservable<_> = x

    member this.Yield (x : 'a) : AsyncObservable<_> = single x

    member this.Bind(source: AsyncObservable<'a>, fn: 'a -> AsyncObservable<'b>) = flatMap fn source

    member x.For(source:AsyncObservable<_>, func) = flatMap func source

// Query builder for an async reactive event source
let reac = new QueryBuilder()
