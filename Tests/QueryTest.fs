module Tests.Query

open System.Threading.Tasks

open ReAction
open ReAction.Query

open NUnit.Framework
open FsUnit
open Tests.Utils
open NUnit.Framework

let toTask computation : Task = Async.StartAsTask computation :> _

[<Test>]
let ``test query empty`` () = toTask <| async {
    // Arrange
    let xs = asyncReact {
        ()
    }
    let obv = TestObserver<unit>()

    // Act
    let! dispose = xs obv.OnNext

    // Assert
    try
        let! latest = obv.Await ()
        ()
    with
        | :? TaskCanceledException -> ()

    let actual = obv.Notifications |> Seq.toList
    let expected : Notification<unit> list = [ OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}


[<Test>]
let ``test query map`` () =
    let mapper x =
        async {
            return x * 10
        }

    let xs = ofSeq <| seq { 1 .. 5 }
    let ys = asyncReact {
        //let! n = ofSeq <| seq { 1 .. 5 }
        ()
    }

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
