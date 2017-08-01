
namespace Pinger

open Android.App
open Android.Content
open Android.OS
open Android.Views
open Android.Widget
open Android.Media

type R = Pinger.Resource

[<Activity (Label = "S.O.S")>]
type AlarmActivity() =
    inherit Activity()

    let mutable mp = null

    static member KEY = "fails"

    member private this.GetVib () =
        this.ApplicationContext.GetSystemService(Context.VibratorService) :?> Vibrator

    override this.OnCreate bundle =
        base.OnCreate bundle

        this.SetContentView R.Layout.Alarm

        let textView = this.FindViewById<TextView> R.Id.textViewFails
        textView.Text <- this.Intent.GetStringExtra AlarmActivity.KEY

        mp <- MediaPlayer.Create (this, R.Raw.yeah)
        mp.Start ()

        this.GetVib().Vibrate(7000L)

        let button = this.FindViewById<Button> R.Id.buttonDismiss
        button.Click.Add (fun _ ->
            mp.Stop ()
            this.Finish ())

    override this.OnDestroy () =
        mp.Stop ()
        this.GetVib().Cancel()
        base.OnDestroy ()
