namespace Pinger

open Android.App
open Android.Content
open Android.Views
open Android.Widget

open Core
open Ui

[<Activity (Label = "Pinger", MainLauncher = true, Icon = "@mipmap/icon")>]
type MainActivity () =
    inherit Activity ()

    let mutable (binder : MyBinder) = null
    let mutable editTextServers = null
    let mutable editTextInterval = null
    let mutable button = null

    member private this.GetPref () =
        this.GetPreferences FileCreationMode.Private

    member private this.SaveMonitorInfo (i : MonitorInfo) =
        let pref = this.GetPref()
        let editor = pref.Edit()
        editor.PutString("servers", Ui.servers2text i.Servers) |> ignore
        editor.PutInt("interval", i.Interval) |> ignore
        editor.Commit() |> ignore

    member private this.LoadMonitorInfo () : MonitorInfo =
        let pref = this.GetPref ()
        {   Servers = Ui.text2servers (pref.GetString ("servers", ""))
            Interval = pref.GetInt ("interval", 1) }

    override this.OnCreate bundle =
        base.OnCreate bundle

        this.SetContentView R.Layout.Main

        editTextServers <- this.FindViewById<EditText> R.Id.editTextServers
        editTextInterval <- this.FindViewById<EditText> R.Id.editTextInterval
        button <- this.FindViewById<Button> R.Id.myButton

        button.Click.Add (fun _ ->
            let model = this.BuildModel ()
            let proceed =
                if model.Servers.Enabled then
                    match parseMonitorInfo editTextServers.Text editTextInterval.Text with
                    | Error msg ->
                        Toast.MakeText(this, msg, ToastLength.Short).Show ()
                        false
                    | Ok info ->
                        this.SaveMonitorInfo info
                        binder.Start info
                        true
                else
                    binder.Stop ()
                    true
            if proceed then
                model |> buttonPressed |> this.ApplyModel)

        use i = new Intent (this, typeof<MyService>)
        let scon = new MyServiceConnection(this)
        this.ApplicationContext.BindService(i, scon, Bind.AutoCreate) |> ignore

    interface ServiceCallback with
        member this.OnServiceConnected theBinder =
            binder <- theBinder

            let info, monitoring =
                match binder.GetMonitorInfo () with
                | Some i -> i, true
                | None -> this.LoadMonitorInfo (), false
           
            info2model info monitoring
            |> this.ApplyModel

            if monitoring then
                this.SaveMonitorInfo info
            
        member this.OnServiceDisconnected () =
            this.BuildModel ()
            |> serviceDisconnected
            |> this.ApplyModel
    
    member this.ApplyModel (model : MainModel) =
        editTextServers.Text <- model.Servers.Text
        editTextServers.Enabled <- model.Servers.Enabled

        editTextInterval.Text <- model.Interval.Text
        editTextInterval.Enabled <- model.Interval.Enabled

        button.Text <- model.Button.Text
        button.Enabled <- model.Button.Enabled

    member this.BuildModel () : MainModel =
        {   Servers = state editTextServers.Text editTextServers.Enabled
            Interval = state editTextInterval.Text editTextInterval.Enabled
            Button = state button.Text button.Enabled }
