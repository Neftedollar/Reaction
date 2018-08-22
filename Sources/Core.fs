namespace Reaction

open System.Threading
open Types

module Core =
    let disposableEmpty () =
        async {
            return ()
        }

    let compositeDisposable (ds : AsyncDisposable seq) : AsyncDisposable =
        let cancel () = async {
            for d in ds do
                do! d ()
        }
        cancel

    let canceller () =
        let cancellationSource = new CancellationTokenSource()
        let cancel () = async {
            cancellationSource.Cancel()
            ()
        }

        cancel, cancellationSource.Token

    /// Safe observer that wraps the given observer and makes sure that
    /// the Rx grammar (onNext* (onError|onCompleted)?) is not violated.
    let safeObserver (obv : AsyncObserver<'a>) =
        let agent = MailboxProcessor.Start(fun inbox ->
            let rec messageLoop stopped = async {
                let! n = inbox.Receive()

                if stopped then
                    return! messageLoop stopped

                let! stop = async {
                    match n with
                    | OnNext x ->
                        try
                            do! OnNext x |> obv
                            return false
                        with
                        | ex ->
                            do! OnError ex |> obv
                            return true
                    | _ ->
                        do! obv n
                        return true
                }

                return! messageLoop stop
            }

            messageLoop false
        )
        let safeObv (n : Notification<'a>) = async {
            agent.Post n
        }
        safeObv

    let refCountAgent initial action =
        MailboxProcessor.Start(fun inbox ->
            let rec messageLoop count = async {
                let! cmd = inbox.Receive ()
                let newCount =
                    match cmd with
                    | Increase -> count + 1
                    | Decrease -> count - 1

                if newCount = 0 then
                    do! action
                    return ()

                return! messageLoop newCount
            }

            messageLoop initial
        )
