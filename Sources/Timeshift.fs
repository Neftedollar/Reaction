namespace Reaction

open System
open Types
open Core

module Timeshift =
    // Delays each notification with the given number of milliseconds
    let delay (msecs : int) (source : AsyncObservable<'a>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            let agent = MailboxProcessor.StartImmediate(fun inbox ->
                let rec messageLoop state = async {
                    let! n, dueTime = inbox.Receive()

                    let diff : TimeSpan = dueTime - ReactionContext.Now
                    let msecs = Convert.ToInt32 diff.TotalMilliseconds
                    if msecs > 0 then
                        do! ReactionContext.SleepAsync msecs
                    do! aobv n

                    return! messageLoop state
                }

                messageLoop (0, 0)
            )

            async {
                let obv n =
                    async {
                        let dueTime = ReactionContext.Now + TimeSpan.FromMilliseconds(float msecs)
                        agent.Post (n, dueTime)
                    }
                return! source obv
            }
        subscribe

    let debounce msecs (source : AsyncObservable<'a>) : AsyncObservable<'a> =
        let subscribe (aobv : AsyncObserver<'a>) =
            let safeObserver = safeObserver aobv
            let infinite = Seq.initInfinite (fun index -> index)

            let agent = MailboxProcessor.StartImmediate(fun inbox ->
                let rec messageLoop currentIndex = async {
                    let! n, index = inbox.Receive ()

                    let! newIndex = async {
                        match n, index with
                        | OnNext _, idx when idx = currentIndex ->
                            do! safeObserver n
                            return index
                        | OnNext _, _ ->
                            if index > currentIndex then
                                return index
                            else
                                return currentIndex
                        | _, _ ->
                            do! safeObserver n
                            return currentIndex
                    }
                    return! messageLoop newIndex
                }

                messageLoop -1
            )

            async {
                let indexer = infinite.GetEnumerator ()

                let obv (n : Notification<'a>) =
                    async {
                        indexer.MoveNext () |> ignore
                        let index = indexer.Current
                        agent.Post (n, index)

                        let worker = async {
                            do! ReactionContext.SleepAsync msecs
                            agent.Post (n, index)
                        }

                        Async.StartImmediate worker
                    }
                let! dispose = source obv

                let cancel () =
                    async {
                        do! dispose ()
                        agent.Post (OnCompleted, 0)
                    }
                return cancel
            }
        subscribe
