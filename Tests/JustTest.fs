module Tests.Just

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
let ``Test just happy``() = toTask <| async {
    // Arrange
    let xs = just 42
    let obv = TestObserver<int>()

    // Act
    let! dispose = xs obv.OnNext

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
    let! dispose = xs obv.OnNext
    do! dispose ()

    // Assert
    let actual = obv.Notifications |> Seq.toList
    Assert.That(actual, Is.EquivalentTo([]))
}

