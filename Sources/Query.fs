namespace Reaction

type QueryBuilder() =
    member this.Zero() : AsyncObservable<_> = empty ()

    member this.YieldFrom (xs : AsyncObservable<'a>) : AsyncObservable<'a> = xs

    member this.Yield (x : 'a) : AsyncObservable<'a> = single x

    member this.Bind(source: AsyncObservable<'a>, fn: 'a -> AsyncObservable<'b>) : AsyncObservable<'b> = flatMap fn source

    member x.For(source:AsyncObservable<_>, func) : AsyncObservable<'b> = flatMap func source

[<AutoOpen>]
module Query =
    // Query builder for an async reactive event source
    let reaction = new QueryBuilder()
    let rx = reaction // Shorter alias
