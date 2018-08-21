module Tests.Query

open System.Threading.Tasks

open ReAction
open ReAction.Query

open NUnit.Framework
open FsUnit

open Tests.Utils

let toTask computation : Task = Async.StartAsTask computation :> _

[<Test>]
let ``test query empty`` () = toTask <| async {
    // Arrange
    let xs = reaction {
        ()
    }
    let obv = TestObserver<unit>()

    // Act
    let! dispose = xs.SubscribeAsync obv.PostAsync

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
let ``test query let!`` () = toTask <| async {
    // Arrange
    let obv = TestObserver<int>()

    let xs = reaction {
        let! a = seq [1; 2] |> ofSeq
        let! b = seq [3; 4] |> ofSeq

        yield a + b
    }

    // Act
    let! subscription = xs.SubscribeAsync obv.PostAsync
    let! latest = obv.Await ()

    // Assert
    let actual = obv.Notifications |> Seq.toList
    let expected : Notification<int> list = [ OnNext 4; OnNext 5; OnNext 5; OnNext 6; OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}