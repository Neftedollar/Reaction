module Tests.Map

open System.Threading.Tasks

open AsyncReactive.Types
open AsyncReactive.Core

open NUnit.Framework
open FsUnit
open Tests.Utils
open NUnit.Framework
open System

let toTask computation : Task = Async.StartAsTask computation :> _

[<Test>]
let ``Test map``() = toTask <| async {
    // Arrange
    let mapper x =
        async {
            return x * 10
        }

    let xs = just 42 |> map mapper
    let obv = TestObserver<int>()

    // Act
    let! sub = xs obv.OnNext
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

    let xs = just "error" |> map mapper
    let obv = TestObserver<unit>()

    // Act
    let! sub = xs obv.OnNext

    do! Async.Sleep(100)

    // Assert
    obv.Notifications |> should haveCount 1
    let actual = obv.Notifications |> Seq.toList
    let expected : Notification<unit> list = [ OnError error ]
    Assert.That(actual, Is.EquivalentTo(expected))
}