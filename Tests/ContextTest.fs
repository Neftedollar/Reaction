module Tests.Context

open System
open System.Threading
open System.Threading.Tasks

open NUnit.Framework
open FsUnit

open Reaction
open Test.Reaction.Context

let toTask computation : Task = Async.StartAsTask computation :> _

[<Test>]
let test_scheduler1 () = toTask <| async {
    // Arrange
    let mutable ran = false

    let ctx = TestSynchronizationContext ()

    let task = async {
        ran <- true
    }
    // Act
    do! ctx.RunAsync task

    // Assert
    Assert.IsTrue ran
}

[<Test>]
let test_scheduler2 () = toTask <| async {
    // Arrange
    let ctx = TestSynchronizationContext ()
    let mutable ran = false

    let task = async {
        do! ReactionContext.SleepAsync 100
        ran <- true
    }
    // Act
    do! ctx.RunAsync task

    // Assert
    Assert.IsTrue ran
}
