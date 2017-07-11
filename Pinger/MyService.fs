namespace Pinger

open Android.App
open Android.Content
open Android.OS
open System

[<AllowNullLiteral>]
type MyBinder (agent : Agent) =
    inherit Binder ()

    member this.Start info =
        agent.Start info

    member this.Stop () =
        agent.Stop ()

    member this.CurrentMonitorInfo =
        agent.CurrentMonitorInfo

[<Service>]
type MyService () =
    inherit Service ()

    let mutable agent = null
    let mutable binder = null

    override this.OnCreate () =
        log "MyService.OnCreate"
        agent <- Agent (fun fails ->
            use i = new Intent (this, typeof<AlarmActivity>)

            i.AddFlags (ActivityFlags.NewTask) |> ignore

            let time = sprintf "[%s]" (DateTime.Now.ToString ())

            i.PutExtra (AlarmActivity.KEY, time + " Failed servers: " + (makeTextServers fails)) |> ignore

            this.StartActivity i)

        binder <- new MyBinder (agent)

    override this.OnBind intent =
        log "MyService.OnBind"
        binder :> IBinder

    override this.OnDestroy () =
        log "MyService.OnDestroy"
        agent.Exit ()
