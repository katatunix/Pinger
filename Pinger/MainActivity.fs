namespace Pinger

open Android.App
open Android.Content
open Android.Views
open Android.Widget

[<Activity (Label = "Pinger", MainLauncher = true, Icon = "@mipmap/icon")>]
type MainActivity () =
    inherit Activity ()

    let mutable (binder : MyBinder) = null
    let mutable isMonitoring = false

    let mutable editTextServers = null
    let mutable editTextInterval = null
    let mutable myButton = null

    member private this.GetPref () =
        this.GetPreferences(FileCreationMode.Private)

    member private this.SaveInfo info =
        let sp = this.GetPref()
        let editor = sp.Edit()
        editor.PutString("servers", makeTextServers info.Servers) |> ignore
        editor.PutInt("interval", info.IntervalMins) |> ignore
        editor.Commit() |> ignore

    member private this.LoadInfo =
        let sp = this.GetPref()
        sp.GetString("servers", ""),
        sp.GetInt("interval", 1)

    override this.OnCreate bundle =
        base.OnCreate bundle

        this.SetContentView R.Layout.Main

        editTextServers <- this.FindViewById<EditText> R.Id.editTextServers
        editTextInterval <- this.FindViewById<EditText> R.Id.editTextInterval

        myButton <- this.FindViewById<Button> R.Id.myButton
        myButton.Click.Add (fun _ ->
            if isMonitoring then
                binder.Stop ()
                isMonitoring <- false
                editTextServers.Enabled <- true
                editTextInterval.Enabled <- true
                this.makeButtonStart ()
            else
                let info = parseMonitorInfo editTextServers.Text editTextInterval.Text
                if info.Servers.IsEmpty then
                    Toast.MakeText(this, "No server to monitor", ToastLength.Short).Show()
                elif info.IntervalMins = 0 then
                    Toast.MakeText(this, "Invalid interval", ToastLength.Short).Show()
                else
                    this.SaveInfo info
                    binder.Start info
                    isMonitoring <- true
                    editTextServers.Enabled <- false
                    editTextInterval.Enabled <- false
                    this.makeButtonStop ())

        use i = new Intent (this, typeof<MyService>)
        let scon = new MyServiceConnection (this)
        this.ApplicationContext.BindService (i, scon, Bind.AutoCreate) |> ignore

    member private this.makeButtonStart () =
        myButton.Text <- "Start"

    member private this.makeButtonStop () =
        myButton.Text <- "Stop"

    override this.OnDestroy () =
        base.OnDestroy ()

    interface ServiceCallback with
        member this.OnServiceConnected theBinder =
            binder <- theBinder

            match binder.CurrentMonitorInfo with
            | None ->
                isMonitoring <- false
                let strServers, interval = this.LoadInfo

                editTextServers.Enabled <- true
                editTextServers.Text <- strServers

                editTextInterval.Enabled <- true
                editTextInterval.Text <- string interval

                this.makeButtonStart ()

            | Some info ->
                isMonitoring <- true

                editTextServers.Enabled <- false
                editTextServers.Text <- makeTextServers info.Servers

                editTextInterval.Enabled <- false
                editTextInterval.Text <- string info.IntervalMins

                this.makeButtonStop ()

                this.SaveInfo info

                binder.Start info
            
        member this.OnServiceDisconnected () =
            binder <- null
            editTextServers.Enabled <- false
            editTextInterval.Enabled <- false
            myButton.Enabled <- false
            myButton.Text <- "Close this app and open again!"
