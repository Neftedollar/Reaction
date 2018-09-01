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

- **empty** : () -> AsyncObservable<'a>
- **single** : 'a -> AsyncObservable<'a>
- **fail** : exn -> AsyncObservable<'a>
- **ofSeq** : seq< 'a> -> AsyncObservable<'a>
- **map** : ('a -> 'b) -> AsyncObservable<'a> -> AsyncObservable<'b>
- **mapi** : ('a*int -> 'b) -> AsyncObservable<'a> -> AsyncObservable<'b>
- **mapAsync** : ('a -> Async<'b>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- **mapiAsync** : ('a*int -> Async<'b>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- **filter** : ('a -> Async\<bool\>) -> AsyncObservable<'a> -> AsyncObservable<'a>
- **filterAsync** : ('a -> bool) -> AsyncObservable<'a> -> AsyncObservable<'a>
- **distinctUntilChanged** : AsyncObservable<'a> -> AsyncObservable<'a>
- **scan** : 's -> ('s -> 'a -> 's) -> AsyncObservable<'a> -> AsyncObservable<'s>
- **scanAsync** : 's -> ('s -> 'a -> Async<'s>) -> AsyncObservable<'a> -> AsyncObservable<'s>
- **merge** : AsyncObservable<AsyncObservable<'a>> -> AsyncObservable<'a>
- **switchLatest** : AsyncObservable<AsyncObservable<'a>> -> AsyncObservable<'a>
- **flatMap** : ('a -> AsyncObservable<'b>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- **flatMapi** : ('a*int -> AsyncObservable<'b>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- **flatMapAsync** : ('a -> Async<AsyncObservable<'b>>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- **flatMapiAsync** : ('a*int -> Async<AsyncObservable<'b>>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- **flatMapLatest** : ('a -> AsyncObservable<'b>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- **flatMapLatestAsync** : ('a -> Async<AsyncObservable<'b>>) -> AsyncObservable<'a> -> AsyncObservable<'b>
- **concat** : seq<AsyncObservable<'a>> -> AsyncObservable<'a>
- **startWith** : seq<'a> -> AsyncObservable<'a> -> AsyncObservable<'a>
- **delay** : int -> AsyncObservable<'a> -> AsyncObservable<'a>
- **debounce** : int -> AsyncObservable<'a> -> AsyncObservable<'a>
- **combineLatest** : AsyncObservable<'b> -> AsyncObservable<'a> -> AsyncObservable<'a*'b>
- **withLatestFrom** : AsyncObservable<'b> -> AsyncObservable<'a> -> AsyncObservable<'a*'b>
- **catch** : (exn -> AsyncObservable<'a>) -> AsyncObservable<'a> -> AsyncObservable<'a>
- **groupBy** : ('a -> 'g) -> AsyncObservable<'a> -> AsyncObservable<AsyncObservable<'a>>
- **zipSeq** : seq<'b> -> AsyncObservable<'a> -> AsyncObservable<'a*'b>
- **choose** : ('a -> 'b option) -> AsyncObservable<'a> -> AsyncObservable<'b>
