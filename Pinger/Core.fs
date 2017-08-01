namespace Pinger

open System
open System.Net
open System.Net.Sockets

open Android.Util

[<AutoOpen>]
module Core =

    let log s = Log.Debug ("Pinger", s) |> ignore
    //let log2 (fmt : Printfn.Str

    type Server = { Address : string; Port : int }

    let makeTextServers servers =
        servers
        |> List.map (fun sv -> sprintf "%s:%d" sv.Address sv.Port)
        |> String.concat ", "

    let parseInt i =
        match Int32.TryParse i with
        | true, x -> x
        | _ -> 0

    let parseServers (str : string) =
        str.Split ([| ','; '\n' |], StringSplitOptions.RemoveEmptyEntries)
        |> List.ofArray
        |> List.map (fun server ->
            let arr = server.Trim().Split ':'
            let address = arr.[0]
            let port = if arr.Length = 2 then parseInt arr.[1] else 0
            { Address = address; Port = port })

    let parseInterval = parseInt

    type MonitorInfo = {
        Servers : Server list;
        IntervalMins : int }

    let parseMonitorInfo strServers strInterval =
        {   Servers = parseServers strServers
            IntervalMins = parseInterval strInterval }

    let tryConnect (servers : Server list) =
        servers
        |> List.filter (fun server ->
            try
                use client = new TcpClient ()
                log (sprintf "Connect to: %s:%d" server.Address server.Port)
                let result =
                    match IPAddress.TryParse server.Address with
                    | true, ip ->
                        log "Connect as IP address"
                        client.BeginConnect (ip, server.Port, null, null)
                    | _ ->
                        log "Connect as host name"
                        client.BeginConnect (server.Address, server.Port, null, null)

                let notTimeout = result.AsyncWaitHandle.WaitOne (TimeSpan.FromSeconds 10.0)
                let success = notTimeout && client.Connected

                log (if success then "SUCCESSFUL" else "FAILED: Timeout")

                client.Close ()

                not success
            with ex ->
                log ("FAILED: " + ex.Message)
                true)

    type private Msg =
        | Start of MonitorInfo
        | Stop
        | Exit
        | Get of AsyncReplyChannel<MonitorInfo option>

    [<AllowNullLiteral>]
    type Agent (alarm) =

        let mp = MailboxProcessor.Start(fun inbox ->

            let rec start (info : MonitorInfo) =
                async {
                    log "start ..."
                    let! msgOp = inbox.TryReceive (info.IntervalMins * 60000)
                    match msgOp with
                    | None ->
                        let getFails () = tryConnect info.Servers
                        let fails =
                            [ 1..2 ]

                            |> List.fold
                                (fun fails _ ->
                                    if fails |> List.isEmpty then
                                        fails
                                    else
                                        getFails ())
                                (getFails ())
                        
                        if not fails.IsEmpty then
                            alarm fails
                        return! start info

                    | Some msg ->
                        match msg with
                        | Start info -> return! start info
                        | Stop -> return! stop ()
                        | Exit -> return ()
                        | Get replyChannel ->
                            replyChannel.Reply (Some info)
                            return! start info }

            and stop () =
                async {
                    log "stop ..."
                    let! msg = inbox.Receive ()
                    match msg with
                    | Start info -> return! start info
                    | Stop -> return! stop ()
                    | Exit -> return ()
                    | Get replyChannel ->
                        replyChannel.Reply None
                        return! stop () }

            stop ())

        member this.Start info =
            mp.Post (Start info)

        member this.Stop () =
            mp.Post Stop

        member this.Exit () =
            mp.Post Exit

        member this.CurrentMonitorInfo =
            mp.PostAndReply (fun replyChannel -> Get replyChannel)
