module Tests.Basic

open System.Threading.Tasks

open AsyncReactive.Core
open AsyncReactive.Types

open NUnit.Framework
open FsUnit
open Tests.Utils
open NUnit.Framework

let toTask computation : Task = Async.StartAsTask computation :> _

[<Test>]
let ``Test just happy``() = toTask <| async {
    // Arrange
    let xs = just 42
    let obv = TestObserver<int>()

    // Act
    let! dispose = xs obv.OnNext
    do! Async.Sleep(200)

    // Assert
    let actual = obv.Notifications |> Seq.toList
    let expected = [ OnNext 42; OnCompleted ]

    Assert.That(actual, Is.EquivalentTo(expected))
}

[<Test>]
let ``Test just dispose after subscribe``() = toTask <| async {
    // Arrange
    let xs = just 42
    let obv = TestObserver<int>()

    // Act
    let! dispose = xs obv.OnNext
    do! dispose ()
    do! Async.Sleep(200)

    // Assert
    let actual = obv.Notifications |> Seq.toList

    Assert.That(actual, Is.EquivalentTo([]))
}

[<Test>]
let ``Test from happy``() = toTask <| async {
    // Arrange
    let xs = from <| seq { 1 .. 5 }
    let obv = TestObserver<int>()

    // Act
    let! dispose = xs obv.OnNext
    do! Async.Sleep(200)

    // Assert
    let actual = obv.Notifications |> Seq.toList
    let expected = [ OnNext 1; OnNext 2; OnNext 3; OnNext 4; OnNext 5; OnCompleted ]

    Assert.That(actual, Is.EquivalentTo(expected))
}

[<Test>]
let ``Test from dispose after subscribe``() = toTask <| async {
    // Arrange
    let xs = from <| seq { 1 .. 5 }
    let obv = TestObserver<int>()

    // Act
    let! dispose = xs obv.OnNext
    do! dispose ()

    do! Async.Sleep(200)

    // Assert
    let actual = obv.Notifications |> Seq.toList

    Assert.That(actual, Is.EquivalentTo([]))
}

[<Test>]
let ``Test map``() = toTask <| async {
    let mapper x =
        async {
            return x * 10
        }

    let xs = just 42 |> map mapper

    let obv = TestObserver<int>()

    let! sub = xs obv.OnNext

    do! Async.Sleep(100)

    obv.Notifications |> should haveCount 2
    Assert.That(obv.Notifications, Is.EquivalentTo([ OnNext 420; OnCompleted ]))
}

[<Test>]
let test_filter () =
    let mapper x =
        async {
            return x * 10
        }

    let predicate a =
        async {
            return a > 10
        }

    let xs = just 42 |> map mapper |> filter predicate

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
