namespace Pinger

open System
open System.Text

open Core

module Ui =

    type State = {
        Text : string
        Enabled : bool }

    let state text e = { Text = text; Enabled = e }

    type MainModel = {
        Servers : State
        Interval : State
        Button : State }

    let servers2text servers =
        servers
        |> List.map (fun sv -> sprintf "%s:%d" sv.Address sv.Port)
        |> String.concat ", "

    let text2int text =
        match Int32.TryParse text with
        | true, x -> x
        | _ -> 0

    let text2servers (text : string) =
        text.Split ([| ','; '\n' |], StringSplitOptions.RemoveEmptyEntries)
        |> List.ofArray
        |> List.map (fun serverText ->
            let p = serverText.Trim().Split ':'
            let address = p.[0]
            let port = if p.Length = 2 then text2int p.[1] else 0
            { Address = address; Port = port })

    let parseMonitorInfo serversText intervalText =
        let servers = text2servers serversText
        let interval = text2int intervalText
        if servers.IsEmpty then
            Error "No server to monitor"
        elif interval <= 0 || interval > 60 then
            Error "Invalid interval, must be in [1..60]"
        else
            Ok { Servers = servers; Interval = interval }

    let info2model (info : MonitorInfo) (monitoring : bool) =
        {   Servers = state (servers2text info.Servers) (not monitoring)
            Interval = state (string info.Interval) (not monitoring)
            Button = state (if monitoring then "Stop" else "Start") true }

    let buttonPressed (model : MainModel) =
        let e = not model.Servers.Enabled
        { model with
            Servers = { model.Servers with Enabled = e }
            Interval = { model.Interval with Enabled = e }
            Button = { model.Button with Text = if e then "Start" else "Stop" } }

    let serviceDisconnected model =
        { model with
            Servers = { model.Servers with Enabled = false }
            Interval = { model.Interval with Enabled = false }
            Button = state "Oop! Please exit and open again!" true }
