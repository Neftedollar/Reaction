module AsyncReactive.Query

open AsyncReactive.Types
open AsyncReactive.Core

type QueryBuiler() =
    member this.ReturnFrom (x) = just x
    [<CustomOperation("select", AllowIntoPattern=true)>]
    member this.Select (s:AsyncObservable<_>, [<ProjectionParameter>] selector : AsyncMapper<_,_>) = map selector
    [<CustomOperation("where", MaintainsVariableSpace=true, AllowIntoPattern=true)>]
    member this.Where (s:AsyncObservable<_>, [<ProjectionParameter>] predicate : _ -> Async<bool> ) = filter predicate s

let query = new QueryBuiler()