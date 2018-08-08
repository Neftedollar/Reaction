# Re:action

Re:action is a lightweight Elmish-ish and Async Reactive (Rx) library for F#, [Fable](http://fable.io/) and [React](https://reactjs.org/).

Currenty a playground project for experimenting with writing MVU-based web applications using async reactive functional programming (Async Observables) in F#. The project is heavely inspired by [Elm](http://elm-lang.org/) and [Elmish](https://elmish.github.io/) but currently a separate project since it does not have any dependencies to any of these projects.

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