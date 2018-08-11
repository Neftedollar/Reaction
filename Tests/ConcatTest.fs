module Tests.Concat

open System
open System.Threading.Tasks

open ReAction

open NUnit.Framework
open FsUnit
open Tests.Utils
open NUnit.Framework

let toTask computation : Task = Async.StartAsTask computation :> _

[<Test>]
let ``Test concat emtpy empty``() = toTask <| async {
    // Arrange
    let xs = empty ()
    let ys = empty ()
    let zs = concat [ xs; ys ]
    let obv = TestObserver<int>()

    // Act
    let! sub = zs obv.OnNext
    try
        do! obv.AwaitIgnore ()
    with
    | _ -> ()

    // Assert
    obv.Notifications |> should haveCount 1
    let actual = obv.Notifications |> Seq.toList
    let expected : Notification<int> list = [ OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}

[<Test>]
let ``Test concat non emtpy empty``() = toTask <| async {
    // Arrange
    let xs = from <| seq { 1..3 }
    let ys = empty ()
    let zs = concat [ xs; ys ]
    let obv = TestObserver<int>()

    // Act
    let! sub = zs obv.OnNext
    let! result = obv.Await ()

    // Assert
    result |> should equal 3
    obv.Notifications |> should haveCount 4
    let actual = obv.Notifications |> Seq.toList
    let expected = [ OnNext 1; OnNext 2; OnNext 3; OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}

[<Test>]
let ``Test concat empty non empty``() = toTask <| async {
    // Arrange
    let xs = empty ()
    let ys = from <| seq { 1..3 }
    let zs = concat [ xs; ys ]
    let obv = TestObserver<int>()

    // Act
    let! sub = zs obv.OnNext
    let! result = obv.Await ()

    // Assert
    result |> should equal 3
    obv.Notifications |> should haveCount 4
    let actual = obv.Notifications |> Seq.toList
    let expected = [ OnNext 1; OnNext 2; OnNext 3; OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}

[<Test>]
let ``Test concat two``() = toTask <| async {
    // Arrange
    let xs = from <| seq { 1..3 }
    let ys = from <| seq { 4..6 }
    let zs = concat [ xs; ys ]
    let obv = TestObserver<int>()

    // Act
    let! sub = zs obv.OnNext
    let! result = obv.Await ()

    // Assert
    result |> should equal 6
    obv.Notifications |> should haveCount 7
    let actual = obv.Notifications |> Seq.toList
    let expected = [ OnNext 1; OnNext 2; OnNext 3; OnNext 4; OnNext 5; OnNext 6; OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}

[<Test>]
let ``Test concat three``() = toTask <| async {
    // Arrange
    let a = from <| seq { 1..2 }
    let b = from <| seq { 3..4 }
    let c = from <| seq { 5..6 }
    let xs = concat [ a; b; c ]
    let obv = TestObserver<int>()

    // Act
    let! sub = xs obv.OnNext
    let! result = obv.Await ()

    // Assert
    result |> should equal 6
    obv.Notifications |> should haveCount 7
    let actual = obv.Notifications |> Seq.toList
    let expected = [ OnNext 1; OnNext 2; OnNext 3; OnNext 4; OnNext 5; OnNext 6; OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}