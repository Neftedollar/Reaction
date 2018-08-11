# ¡Re:action!

Re:action is a lightweight Async Reactive ([Rx](http://reactivex.io/)) [Elmish](https://elmish.github.io/)-ish library for F# targeting [Fable](http://fable.io/) and [React](https://reactjs.org/). This means that the code is [transpiled](https://en.wikipedia.org/wiki/Source-to-source_compiler) to JavaScript and thus the same code may be used both client and server side for full stack software development.

Currently a playground project for experimenting with MVU-based web applications using async reactive functional programming (Async Observables) in F#. The project is heavily inspired by [Elm](http://elm-lang.org/) and [Elmish](https://elmish.github.io/) but currently a separate project since it does not have any dependencies to any of these projects. The inspiration for Async Observables and plain old functions (POF) comes from my own work with [aioreactive](https://github.com/dbrattli/aioreactive).

## Async Observables

The difference between an "Async Observable" and an "Observable" is that with "Async Observables" you need to await operations such as subscribe, OnNext, OnError, OnCompleted. Also mappers may be async. This means create operations may do async operations i.e setup network connections, and observers may do async operations such as writing to disk.

Re:action uses simple functions instead of classes and the traditional Rx interfaces. Some of the operators uses mailbox processors (actors) to implement the observer pipeline in order to avoid locks and mutables. This makes the code more Fable friendly for easily transpiling to JavaScript. Here are the core types:

```f#
type Notification<'a> =
    | OnNext of 'a
    | OnError of exn
    | OnCompleted

type AsyncDisposable = unit -> Async<unit>
type AsyncObserver<'a> = Notification<'a> -> Async<unit>
type AsyncObservable<'a> = AsyncObserver<'a> -> Async<AsyncDisposable>

type AsyncMapper<'a, 'b> = 'a -> Async<'b>
type AsyncPredicate<'a> = 'a -> Async<bool>
type AsyncAccumulator<'s, 't> = 's -> 't -> Async<'s>
```

## Elmish-ish example

Reactive [MVU archtecture](https://guide.elm-lang.org/architecture/) example ([source code](https://github.com/dbrattli/Re-action/tree/master/examples/Timeflies)) using Re:action for impl.
the classic Time Flies example from RxJS. No jQuery or other js-libraries in this example. This code
is very simplar to Elmish but the difference is that we can compose powerful reactive
queries to transform, filter, aggregate and time-shift our stream of messages.

```f#
// Messages for updating the model
type Msg =
    | MouseEvent of MouseEvent
    | LetterMove of int * char * float * float

// Model (state) for updating the view
type Model = {
    Pos: Map<int, char * float * float>
}

let update (currentModel : Model) (msg : Msg) =
    match currentModel.Pos, msg with
    | _, LetterMove (i, c, x, y) ->
        { currentModel with Pos = currentModel.Pos.Add (i, (c, x, y)) }
    | _ ->
        currentModel

// View for being observed by React
let view (model : Model) =
    let letters = model.Pos

    div [ Style [ FontFamily "Consolas, monospace"]] [
        for KeyValue(i, values) in letters do
            let c, x, y = values
            yield span [ Style [Top y; Left x; Position "absolute"] ] [
                str (string c)
            ]
    ]

let main = async {
    let initialModel = { Pos = Map.empty }

    // Query
    let moves = from <| Seq.toList "TIME FLIES LIKE AN ARROW"
                |> flatMapIndexed (fun x i ->
                        fromMouseMoves ()
                        |> delay (100.0 * float i)
                        |> map (fun m -> LetterMove (i, x, m.clientX + float i * 10.0 + 15.0, m.clientY))
                   )
                |> scan initialModel update
                |> map view

    // React observer
    let render n =
        async {
            match n with
            | OnNext dom -> renderReact "elmish-app" dom
            | _ -> ()
        }

    // Subscribe
    do! moves render |> Async.Ignore
}

main |> Async.StartImmediate
```