module Tests.From

open System.Threading.Tasks

open AsyncReactive

open NUnit.Framework
open FsUnit
open Tests.Utils

let toTask computation : Task = Async.StartAsTask computation :> _

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
