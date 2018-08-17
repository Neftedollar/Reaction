// TODO: move to a separate project (assembly)

namespace ReAction


#if FABLE_COMPILER
open Fable
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Import.Browser
#else
type MouseEvent() =
    member this.clientX = 0.0
    member this.clientY = 0.0
#endif

[<AutoOpen>]
module Fable =
#if FABLE_COMPILER
    let fromMouseMoves () : AsyncObservable<MouseEvent> =
        let subscribe (obv : Types.AsyncObserver<MouseEvent>) : Async<AsyncDisposable> =
            async {
                let onMouseMove (ev : Fable.Import.Browser.MouseEvent) =
                    async {
                        do! obv.OnNext ev
                    } |> Async.StartImmediate

                window.addEventListener_mousemove onMouseMove
                let cancel () = async {
                    window.removeEventListener ("mousemove", unbox onMouseMove)
                }
                return AsyncDisposable cancel
            }

        AsyncObservable subscribe

    /// Setup rendering of root React component inside html element identified by placeholderId
    let renderReact placeholderId view =
        let mutable lastRequest = None

        match lastRequest with
        | Some r -> window.cancelAnimationFrame r
        | _ -> ()

        lastRequest <- Some (window.requestAnimationFrame (fun _ ->
            Fable.Import.ReactDom.render(
                view,
                document.getElementById(placeholderId)
            )))

#else
    let fromMouseMoves () : AsyncObservable<MouseEvent> =
        empty ()
    let renderReact placeholderId view =
        ()
#endif
