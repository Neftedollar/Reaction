module Tests.From

open System.Threading.Tasks

open ReAction

open NUnit.Framework
open FsUnit
open Tests.Utils

let toTask computation : Task = Async.StartAsTask computation :> _

[<Test>]
let ``Test from empty``() = toTask <| async {
    // Arrange
    let xs = from <| Seq.empty
    let obv = TestObserver<int>()

    // Act
    let! dispose = xs.Subscribe obv.OnNotification

    do! obv.AwaitIgnore ()

    // Assert
    let actual = obv.Notifications |> Seq.toList
    let expected : Notification<int> list = [ OnCompleted ]

    Assert.That(actual, Is.EquivalentTo(expected))
}

[<Test>]
let ``Test from non empty``() = toTask <| async {
    // Arrange
    let xs = from <| seq { 1 .. 5 }
    let obv = TestObserver<int>()

    // Act
    let! dispose = xs.Subscribe obv.OnNotification
    do! obv.AwaitIgnore ()

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
    let! dispose = xs.Subscribe obv.OnNotification
    do! dispose ()

    // Assert
    //let actual = obv.Notifications |> Seq.toList
    //Assert.That(actual, Is.EquivalentTo([]))
}

