module Client

open Fable.Helpers.React
open Fable.Helpers.React.Props

open ReAction

open Fable.Import.React
open Fable

// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type Model = {
    Pos: Map<int, string * int * int>
}

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
    | MouseEvent of MouseEvent
    | LetterMove of int * string * int * int

// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (currentModel : Model) (msg : Msg) : Async<Model> = async {
    match currentModel.Pos, msg with
    | _, LetterMove (i, c, x, y) ->
        return { currentModel with Pos = currentModel.Pos.Add (i, (c, x, y)) }
    | _ ->
        return currentModel
}

let view (model : Model, i) = async {
    let letters = model.Pos
    let offsetX x i = x + i * 10 + 15

    return div [ Style [ FontFamily "Consolas, monospace"]] [
        for KeyValue(i, (c, x, y)) in letters do
            yield span [ Style [Top y; Left (offsetX x i); Position "absolute"] ] [
                str c
            ]
    ]
}

let main = async {
    let initialModel = { Pos = Map.empty }

    let moves =
        Seq.toList "TIME FLIES LIKE AN ARROW" |> Seq.map string |> from
            >>= fun (x, i) -> async {
                    return fromMouseMoves ()
                    |> delay (100 * i)
                    |> map (fun (m, _) -> async {
                        printfn "%A" (i, x, m.clientX, m.clientY)
                        return LetterMove (i, x, int m.clientX, int m.clientY)
                    })
                }
            |> scan initialModel update
            |> map view

    let render n =
        async {
            match n with
            | OnNext dom -> renderReact "elmish-app" dom
            | _ -> ()
        }

    do! moves.Subscribe render |> Async.Ignore
}

main |> Async.StartImmediate
