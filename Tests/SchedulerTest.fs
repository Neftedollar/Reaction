module Tests.Scheduler

open System
open System.Threading
open System.Threading.Tasks

open AsyncReactive.Types
open AsyncReactive.Core
open AsyncReactive.Query

open NUnit.Framework
open FsUnit
open Tests.Utils
open NUnit.Framework

let runTask computation : unit =
    let func = Func<Task>(fun _ -> (Async.StartAsTask computation :> _))
    printfn "runTask"
    TestScheduler.Run func

let toTask computation : Task = Async.StartAsTask computation :> _

[<Test>]
let test_scheduler1 () = toTask <| async {
    printfn "Test running 2"
    let scheduler = TestScheduler ()
    do! Async.SwitchToContext(scheduler)
    do! Async.Sleep 100
    printfn "Test running 3"
    ()
    Assert.IsTrue false
}

[<Test>]
let test_scheduler2 () =
    printfn "Test running"

    runTask <| async {
        printfn "Test running 2"
        do! Async.Sleep 100
        printfn "Test running 3"
        ()
    }

    Assert.IsTrue true