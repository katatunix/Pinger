namespace Pinger

open Android.Content

type ServiceCallback =
    abstract OnServiceConnected : MyBinder -> unit
    abstract OnServiceDisconnected : unit -> unit

type MyServiceConnection (callback : ServiceCallback) =
    inherit Java.Lang.Object ()

    interface IServiceConnection with
        member this.OnServiceConnected (name, binder) =
            callback.OnServiceConnected (binder :?> MyBinder)

        member this.OnServiceDisconnected name =
            callback.OnServiceDisconnected ()
