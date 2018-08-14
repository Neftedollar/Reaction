namespace ReAction

#if FABLE_COMPILER

open Fable
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Import.Browser

[<AutoOpen>]
module Fable =
    let fromMouseMoves () : AsyncObservable<MouseEvent> =
        let subscribe (obv : AsyncObserver<MouseEvent>) : Async<AsyncDisposable> =
            async {
                let onMouseMove (ev : Fable.Import.Browser.MouseEvent) =
                    async {
                        do! ev |> OnNext |> obv
                    } |> Async.StartImmediate

                window.addEventListener_mousemove onMouseMove
                let cancel () = async {
                    window.removeEventListener ("mousemove", unbox onMouseMove)
                }
                return cancel
            }

        subscribe
#endif
