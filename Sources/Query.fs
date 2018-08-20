module ReAction.Query

open Types

type QueryBuiler() =
    member this.Zero() = Creation.empty ()

    member this.ReturnFrom (x) = Creation.just x

    member __.Bind(m: AsyncObservable<_>, f: _ -> AsyncObservable<_>) = Transform.flatMap f

    [<CustomOperation("select", AllowIntoPattern=true)>]
    member this.Select (s:AsyncObservable<_>, [<ProjectionParameter>] selector : _ -> _) = Transform.mapAsync selector

    [<CustomOperation("where", MaintainsVariableSpace=true, AllowIntoPattern=true)>]
    member this.Where (s:AsyncObservable<_>, [<ProjectionParameter>] predicate : _ -> Async<bool> ) = Filter.filterAsync predicate s

// Query builder for an async reactive event source
let asyncReact = new QueryBuiler()
