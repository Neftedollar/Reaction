# re·ac·tion

Reaction is a lightweight Async Reactive ([Rx](http://reactivex.io/)) library for F# targeting [Fable](http://fable.io/). This means that the code may be [transpiled](https://en.wikipedia.org/wiki/Source-to-source_compiler) to JavaScript, and thus the same F# code may be used both client and server side for full stack software development.

Currently a playground project for experimenting with async reactive functional programming (Async Observables) in F#. The project is heavily inspired by [aioreactive](https://github.com/dbrattli/aioreactive).

See [Fable Reaction](https://github.com/dbrattli/Fable.Reaction) for Elmish-ish use of Reaction.

## Install

```cmd
paket add Reaction --project <project>
```

## Async Observables

Reaction is an implementation of Async Observable. The difference between an "Async Observable" and an "Observable" is that with "Async Observables" you need to await operations such as Subscribe, OnNext, OnError, OnCompleted. This enables Subscribe to await async operations i.e setup network connections, and observers may finally await side effects such as writing to disk (observers are all about side-effects right?).

Reaction is built upon simple functions instead of classes and the traditional Rx interfaces. Some of the operators uses mailbox processors (actors) to implement the observer pipeline in order to avoid locks and mutables. This makes the code more Fable friendly. Here are the core types:

```f#
type Notification<'a> =
    | OnNext of 'a
    | OnError of exn
    | OnCompleted

type AsyncDisposable = unit -> Async<unit>
type AsyncObserver<'a> = Notification<'a> -> Async<unit>
type AsyncObservable<'a> = AsyncObserver<'a> -> Async<AsyncDisposable>
```

## Usage

```f#
open Reaction

let main = async {
    let mapper x =
        x * 10

    let xs = single 42 |> map mapper
    let obv n =
        match n with
        | OnNext x -> printfn "OnNext: %d" x
        | OnError ex -> printfn "OnError: %s" ex.ToString()
        | OnCompleted -> printfn "OnCompleted"

    let! subscription = xs.SubscribeAsync obv
}

Async.Start main
```

### Operators

The following parameterized async observerable returning functions (operators) are
currently supported. Other operators may be implemented on-demand, but there are
currently no plans to make this into a full featured Rx implementation.

- [x] **empty** : () -> AsyncObservable<'a>
- [x] **single** : 'a -> AsyncObservable<'a>
- [x] **fail** : exn -> AsyncObservable<'a>
- [x] **ofSeq** : seq< 'a> -> AsyncObservable<'a>
- [x] **map** : ('a -> 'b) -> AsyncObservable<'a> -> AsyncObservable<'b>
- [x] **mapi** : ('a*int -> 'b) -> AsyncObservable<'a> -> AsyncObservable<'b>
- [x] **mapAsync** : ('a -> Async<'b>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- [x] **mapiAsync** : ('a*int -> Async<'b>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- [x] **filter** : ('a -> Async\<bool\>) -> AsyncObservable<'a> -> AsyncObservable<'a>
- [x] **filterAsync** : ('a -> bool) -> AsyncObservable<'a> -> AsyncObservable<'a>
- [x] **distinctUntilChanged** : AsyncObservable<'a> -> AsyncObservable<'a>
- [x] **scan** : 's -> ('s -> 'a -> 's) -> AsyncObservable<'a> -> AsyncObservable<'s>
- [x] **scanAsync** : 's -> ('s -> 'a -> Async<'s>) -> AsyncObservable<'a> -> AsyncObservable<'s>
- [x] **merge** : AsyncObservable<AsyncObservable<'a>> -> AsyncObservable<'a>
- [x] **switchLatest** : AsyncObservable<AsyncObservable<'a>> -> AsyncObservable<'a>
- [x] **flatMap** : ('a -> AsyncObservable<'b>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- [x] **flatMapi** : ('a*int -> AsyncObservable<'b>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- [x] **flatMapAsync** : ('a -> Async<AsyncObservable<'b>>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- [x] **flatMapiAsync** : ('a*int -> Async<AsyncObservable<'b>>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- [x] **concat** : seq<AsyncObservable<'a>> -> AsyncObservable<'a>
- [x] **startWith** : seq<'a> -> AsyncObservable<'a> -> AsyncObservable<'a>
- [x] **delay** : int -> AsyncObservable<'a> -> AsyncObservable<'a>
- [x] **debounce** : int -> AsyncObservable<'a> -> AsyncObservable<'a>
- [x] **combineLatest** : AsyncObservable<'b> -> AsyncObservable<'a> -> AsyncObservable<'a*'b>
- [x] **withLatestFrom** : AsyncObservable<'b> -> AsyncObservable<'a> -> AsyncObservable<'a*'b>
- [x] **catch** : (exn -> AsyncObservable<'a>) -> AsyncObservable<'a> -> AsyncObservable<'a>
- [x] **groupBy** : ('a -> 'g) -> AsyncObservable<'a> -> AsyncObservable<AsyncObservable<'a>>
- [x] **zipSeq** : seq<'b> -> AsyncObservable<'a> -> AsyncObservable<'a*'b>
