namespace ReAction

type AsyncDisposable = AsyncDisposable of Types.AsyncDisposable with
        static member Unwrap (AsyncDisposable dsp) : Types.AsyncDisposable = dsp

        member this.DisposeAsync () = AsyncDisposable.Unwrap this ()

