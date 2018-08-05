module Tests.Filter

open System
open System.Threading.Tasks

open AsyncReactive

open NUnit.Framework
open FsUnit
open Tests.Utils
open NUnit.Framework

let toTask computation : Task = Async.StartAsTask computation :> _


[<Test>]
let ``Test filter``() = toTask <| async {
    let mapper x =
        async {
            return x * 10
        }

    let xs = just 42 |> map mapper

    let obv = TestObserver<int>()

    let! sub = xs obv.OnNext

    do! Async.Sleep(200)

    obv.Notifications |> should haveCount 2
    let actual = obv.Notifications |> Seq.toList
    let expected = [ OnNext 420; OnCompleted ]
    Assert.That(actual, Is.EquivalentTo(expected))
}


