module Client

open Fable.Helpers.React
open Fable.Helpers.React.Props

open ReAction

open Fulma
open Fable.Import.React
open Fable


// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type Model = {
    Pos: Map<int, char * float * float>
}

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
    | MouseEvent of MouseEvent
    | LetterMove of int * char * float * float

// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (currentModel : Model) (msg : Msg) =
    match currentModel.Pos, msg with
    | _, LetterMove (i, c, x, y) ->
        { currentModel with Pos = currentModel.Pos.Add (i, (c, x, y)) }
    | _ ->
        currentModel

let safeComponents =
    let components =
        span [ ]
           [
             a [ Href "https://github.com/giraffe-fsharp/Giraffe" ] [ str "Giraffe" ]
             str ", "
             a [ Href "http://fable.io" ] [ str "Fable" ]
             str ", "
             a [ Href "https://elmish.github.io/elmish/" ] [ str "Elmish" ]
             str ", "
             a [ Href "https://mangelmaxime.github.io/Fulma" ] [ str "Fulma" ]
           ]

    p [ ]
        [ strong [] [ str "SAFE Template" ]
          str " powered by: "
          components ]

let printLetters (letters : Map<int, char * float * float>) =
    div [ Style [ FontFamily "Consolas, monospace"]] [
        for KeyValue(i, values) in letters do
            let c, x, y = values
            yield span [ Style [Top y; Left x; Position "absolute"] ] [
                str (string c)
            ]
    ]

let view (model : Model) =
    async {
        return div []
            [ printLetters model.Pos

              Navbar.navbar [ Navbar.Color IsPrimary ]
                [ Navbar.Item.div [ ]
                    [ Heading.h2 [ ]
                        [ str "SAFE Template" ] ] ]

              Footer.footer [ ]
                    [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                        [ safeComponents ] ] ]
    }

open Fable.Import.Browser

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

let main = async {
    let initialModel = { Pos = Map.empty }

    let moves = from <| Seq.toList "TIME FLIES LIKE AN ARROW"
                |> flatMapIndexed (fun x i ->
                        fromMouseMoves ()
                        |> delay (100.0 * float i)
                        |> map (fun m -> LetterMove (i, x, m.clientX + float i * 10.0 + 15.0, m.clientY))
                   )
                |> scan initialModel update
                |> mapAsync view

    let render n =
        async {
            match n with
            | OnNext dom -> renderReact "elmish-app" dom
            | _ -> ()
        }

    do! moves render |> Async.Ignore
}

main |> Async.StartImmediate

