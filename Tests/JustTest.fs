module Tests.Just

open System.Threading.Tasks

open ReAction

open NUnit.Framework
open FsUnit
open Tests.Utils

let toTask computation : Task = Async.StartAsTask computation :> _

[<Test>]
let ``Test just happy``() = toTask <| async {
    // Arrange
    let xs = just 42
    let obv = TestObserver<int>()

    // Act
    let! subscription = xs.Subscribe obv.Callable

    // Assert
    let! latest = obv.Await ()
    latest |> should equal 42

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
    let! subscription = xs.Subscribe obv.Callable
    Async.StartImmediate (subscription.Dispose ())

    // Assert
    //let actual = obv.Notifications |> Seq.toList
    //Assert.That(actual, Is.EquivalentTo([]))
    ()
}

