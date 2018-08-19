# ¡Reaction!

Reaction is a lightweight Async Reactive ([Rx](http://reactivex.io/)) [Elmish](https://elmish.github.io/)-ish library for F# targeting [Fable](http://fable.io/) and [React](https://reactjs.org/). This means that the code is [transpiled](https://en.wikipedia.org/wiki/Source-to-source_compiler) to JavaScript and thus the same code may be used both client and server side for full stack software development.

Currently a playground project for experimenting with MVU-based web applications using async reactive functional programming (Async Observables) in F#. The project is heavily inspired by [Elm](http://elm-lang.org/) and [Elmish](https://elmish.github.io/) but currently a separate project since it does not have any dependencies to any of these projects. The inspiration for Async Observables and plain old functions (POF) comes from my own work with [aioreactive](https://github.com/dbrattli/aioreactive).

## Async Observables

The difference between an "Async Observable" and an "Observable" is that with "Async Observables" you need to await operations such as subscribe, OnNext, OnError, OnCompleted. This enables subscribe in create type operators to do async operations i.e setup network connections, and observers may finally do async operations such as writing to disk (observers are all about side-effects right?).

Reaction uses simple functions instead of classes and the traditional Rx interfaces. Some of the operators uses mailbox processors (actors) to implement the observer pipeline in order to avoid locks and mutables. This makes the code more Fable friendly for easily transpiling to JavaScript. Here are the core types:

```f#
type Notification<'a> =
    | OnNext of 'a
    | OnError of exn
    | OnCompleted

type AsyncDisposable = unit -> Async<unit>
type AsyncObserver<'a> = Notification<'a> -> Async<unit>
type AsyncObservable<'a> = AsyncObserver<'a> -> Async<AsyncDisposable>
```

### Operators

The following parameterized async observerable returning functions (operators) are
currently supported. Other operators may be implemented on-demand, but there are
currently no plans to make this into a full featured Rx implementation.

- map, mapi, mapAsync, mapiAsync
- filter, filterAsync
- scan, scanAsync
- merge
- flatMap, flatMapi, flatMapAsync, flatMapiAsync
- concat
- startWith
- distinctUntilChanged
- delay
- debounce
- combineLatest
- withLatestFrom
- switchLatest

## Elmish Reaction example

Reactive [MVU archtecture](https://guide.elm-lang.org/architecture/) example ([source code](https://github.com/dbrattli/Re-action/tree/master/examples/Timeflies)) using Reaction for impl.
the classic Time Flies example from RxJS. No jQuery or other js-libraries in this example. This code
is very simplar to Elmish but the difference is that we can compose powerful reactive
queries to transform, filter, aggregate and time-shift our stream of messages.

```f#
// Messages for updating the model
type Msg =
    | Letter of int * string * int * int

// Model (state) for updating the view
type Model = {
    Letters: Map<int, string * int * int>
}

let update (currentModel : Model) (msg : Msg) : Model =
    match currentModel.Letters, msg with
    | _, Letter (i, c, x, y) ->
        { currentModel with Letters = currentModel.Letters.Add (i, (c, x, y)) }

// View for being observed by React
let view (model : Model) =
    let letters = model.Letters
    let offsetX x i = x + i * 10 + 15

    div [ Style [ FontFamily "Consolas, monospace"]] [
        for KeyValue(i, (c, x, y)) in letters do
            yield span [ Style [Top y; Left (offsetX x i); Position "absolute"] ] [
                str c
            ]
    ]

let main = async {
    let initialModel = { Letters = Map.empty }

    let moves =
        Seq.toList "TIME FLIES LIKE AN ARROW" |> Seq.map string |> from
            |> flatMapi (fun (x, i) ->
                fromMouseMoves ()
                    |> map (fun m -> Letter (i, x, int m.clientX, int m.clientY))
                    |> delay (100 * i)
            )
            |> scan initialModel update
            |> map view

    // React observer
    let obv = renderReact "elmish-app"

    // Subscribe (ignores the disposable)
    do! moves.RunAsync obv
}
```