module Tests.Query

open System.Threading.Tasks

open AsyncReactive.Types
open AsyncReactive.Core
open AsyncReactive.Query

open NUnit.Framework
open FsUnit
open Tests.Utils
open NUnit.Framework

let toTask computation : Task = Async.StartAsTask computation :> _


[<Test>]
let test_query () =
    let mapper x =
        async {
            return x * 10
        }

    let predicate a =
        async {
            return a > 10
        }

    let xs = from <| seq { 1 .. 5 }
    //let ys = query {}

    let obv n =
        async {
            match n with
            | OnNext x -> printfn "%A" x
            | OnError ex -> printfn "OnError: %s." <| ex.ToString()
            | OnCompleted -> printfn "OnCompleted."
        }

    let main =
        async {
            let! subscription = xs obv
            do! subscription ()
        }

    main |> Async.Start
