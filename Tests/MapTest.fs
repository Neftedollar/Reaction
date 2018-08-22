module Tests.Map

open System.Threading.Tasks

open Reaction

open NUnit.Framework
open FsUnit
open Tests.Utils

let toTask computation : Task = Async.StartAsTask computation :> _

[<Test>]
let ``Test map async``() = toTask <| async {
    // Arrange
    let mapper x =
        async {
            return x * 10
        }

    let xs = single 42 |> mapAsync mapper
    let obv = TestObserver<int>()

    // Act
    let! sub = xs.SubscribeAsync obv.PostAsync
    let! latest= obv.Await ()

    // Assert
    latest |> should equal 420
    obv.Notifications |> should haveCount 2
    let actual = obv.Notifications |> Seq.toList
    let expected : Notification<int> list = [ OnNext 420; OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}

[<Test>]
let ``Test map sync``() = toTask <| async {
    // Arrange
    let mapper x =
        x * 10

    let xs = single 42 |> map mapper
    let obv = TestObserver<int>()

    // Act
    let! sub = xs.SubscribeAsync obv.PostAsync
    let! latest= obv.Await ()

    // Assert
    latest |> should equal 420
    obv.Notifications |> should haveCount 2
    let actual = obv.Notifications |> Seq.toList
    let expected : Notification<int> list = [ OnNext 420; OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}

exception MyError of string

[<Test>]
let ``Test map mapper throws exception``() = toTask <| async {
    // Arrange
    let error = MyError "error"
    let mapper x =
        async {
            raise error
        }

    let xs = single "error" |> mapAsync mapper
    let obv = TestObserver<unit>()

    // Act
    let! cnl = xs.SubscribeAsync obv.PostAsync

    try
        do! obv.AwaitIgnore ()
    with
    | _ -> ()

    // Assert
    obv.Notifications |> should haveCount 1
    let actual = obv.Notifications |> Seq.toList
    let expected : Notification<unit> list = [ OnError error ]
    Assert.That(actual, Is.EquivalentTo(expected))
}