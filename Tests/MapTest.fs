module Tests.Map

open System.Threading.Tasks

open ReAction

open NUnit.Framework
open FsUnit
open Tests.Utils

let toTask computation : Task = Async.StartAsTask computation :> _

[<Test>]
let ``Test map``() = toTask <| async {
    // Arrange
    let mapper x =
        async {
            return x * 10
        }

    let xs = just 42 |> mapAsync mapper
    let obv = TestObserver<int>()

    // Act
    let! sub = xs obv.OnNotification
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

    let xs = just "error" |> mapAsync mapper
    let obv = TestObserver<unit>()

    // Act
    let! cnl = xs obv.OnNotification

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