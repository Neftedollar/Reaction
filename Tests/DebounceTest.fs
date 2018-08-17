module Tests.Debounce

open System.Threading.Tasks

open ReAction

open NUnit.Framework
open FsUnit
open Tests.Utils

let toTask computation : Task = Async.StartAsTask computation :> _

[<Test>]
let ``Test debounce single value``() = toTask <| async {
    // Arrange

    let dispatch, obs = stream ()
    let xs = obs |> debounce 100
    let obv = TestObserver<int>()

    // Act
    let! sub = xs.Subscribe (obv.OnNotification)
    do! dispatch.OnNext 42
    do! Async.Sleep 150
    do! dispatch.OnCompleted ()
    let! latest= obv.Await ()

    // Assert
    latest |> should equal 42
    obv.Notifications |> should haveCount 2
    let actual = obv.Notifications |> Seq.toList
    let expected : Notification<int> list = [ OnNext 42; OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}

[<Test>]
let ``Test debounce two immediate values``() = toTask <| async {
    // Arrange

    let dispatch, obs = stream ()
    let xs = obs |> debounce 100
    let obv = TestObserver<int>()

    // Act
    let! sub = xs.Subscribe obv.OnNotification
    do! dispatch.OnNext 42
    do! dispatch.OnNext 43
    do! Async.Sleep 150
    do! dispatch.OnCompleted ()
    let! latest= obv.Await ()

    // Assert
    latest |> should equal 43
    obv.Notifications |> should haveCount 2
    let actual = obv.Notifications |> Seq.toList
    let expected : Notification<int> list = [ OnNext 43; OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}

[<Test>]
let ``Test debounce two separate values``() = toTask <| async {
    // Arrange

    let dispatch, obs = stream ()
    let xs = obs |> debounce 100
    let obv = TestObserver<int>()

    // Act
    let! sub = xs.Subscribe obv.OnNotification
    do! dispatch.OnNext 42
    do! Async.Sleep 150
    do! dispatch.OnNext 43
    do! Async.Sleep 150
    do! dispatch.OnCompleted ()
    let! latest= obv.Await ()

    // Assert
    latest |> should equal 43
    obv.Notifications |> should haveCount 3
    let actual = obv.Notifications |> Seq.toList
    let expected : Notification<int> list = [ OnNext 42; OnNext 43; OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}