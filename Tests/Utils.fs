module Tests.Utils

open System.Collections.Generic
open AsyncReactive.Types

type TestObserver<'a>() =
    let notifications = new List<Notification<'a>>()

    member this.Notifications = notifications

    member this.OnNext (x : Notification<'a>) =
        async {
            this.Notifications.Add(x)
        }
