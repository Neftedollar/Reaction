module AsyncReactive.Query

open AsyncReactive.Core

type QueryBuiler() =
    member this.Zero() = empty ()

    member this.ReturnFrom (x) = just x

    member __.Bind(m: AsyncObservable<_>, f: _ -> Async<AsyncObservable<_>>) = flatMap f

    [<CustomOperation("select", AllowIntoPattern=true)>]
    member this.Select (s:AsyncObservable<_>, [<ProjectionParameter>] selector : AsyncMapper<_,_>) = map selector

    [<CustomOperation("where", MaintainsVariableSpace=true, AllowIntoPattern=true)>]
    member this.Where (s:AsyncObservable<_>, [<ProjectionParameter>] predicate : _ -> Async<bool> ) = filter predicate s

// Query builder for an async reactive event source
let asyncReact = new QueryBuiler()
