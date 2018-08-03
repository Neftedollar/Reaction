module Tests.Map

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
let ``Test map``() = toTask <| async {
    let mapper x =
        async {
            return x * 10
        }

    let xs = just 42 |> map mapper

    let obv = TestObserver<int>()

    let! sub = xs obv.OnNext

    do! Async.Sleep(100)

    obv.Notifications |> should haveCount 2
    Assert.That(obv.Notifications, Is.EquivalentTo([ OnNext 420; OnCompleted ]))
}
