# Re:action

Re:action is a lightweight Async Reactive (Rx) Elmish-ish library for F# using [Fable](http://fable.io/) and [React](https://reactjs.org/).

Currently a playground project for experimenting with MVU-based web applications using async reactive functional programming (Async Observables) in F#. The project is heavely inspired by [Elm](http://elm-lang.org/) and [Elmish](https://elmish.github.io/) but currently a separate project since it does not have any dependencies to any of these projects.

Re:action uses simple functions instead of classes and the traditional Rx interfaces. Some of the operators uses mailbox processors (actors) to implement the observer pipeline in order to avoid locks and mutables. This makes the code much more Fable friendly so the code can be easily transpiled to JavaScript.

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

Example query for Time Flies example:

```f#
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
```