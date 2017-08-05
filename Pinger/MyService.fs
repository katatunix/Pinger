namespace Pinger

open Android.App
open Android.Content
open Android.OS
open System

open Core

[<AllowNullLiteral>]
type MyBinder (agent : Agent) =
    inherit Binder ()
    member this.Start info = agent.Start info
    member this.Stop () = agent.Stop ()
    member this.GetMonitorInfo () = agent.GetMonitorInfo ()

[<Service>]
type MyService () =
    inherit Service ()

    let mutable agent = null
    let mutable binder = null

    override this.OnCreate () =
        agent <- Agent (fun text ->
            use i = new Intent (this, typeof<AlarmActivity>)
            i.AddFlags (ActivityFlags.NewTask) |> ignore
            i.PutExtra (AlarmActivity.KEY, Ui.servers2text text) |> ignore
            this.StartActivity i)
        binder <- new MyBinder (agent)

    override this.OnBind intent =
        binder :> IBinder

    override this.OnDestroy () =
        agent.Exit ()

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
