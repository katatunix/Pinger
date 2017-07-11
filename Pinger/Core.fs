namespace Pinger

open System
open System.Net
open System.Net.Sockets

open Android.Util

[<AutoOpen>]
module Core =

    let log s = Log.Debug ("Pinger", s) |> ignore

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
            let arr = server.Split ':'
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
                let success = result.AsyncWaitHandle.WaitOne (TimeSpan.FromSeconds 10.0)

                log (if success then "SUCCESSFUL" else "FAILED: Timeout")
                if success then
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

            let rec start (info : MonitorInfo) failedCount =
                async {
                    log "start ..."
                    let! msgOp = inbox.TryReceive (info.IntervalMins * 60000)
                    match msgOp with
                    | None ->
                        let fails = tryConnect info.Servers
                        if fails.IsEmpty then
                            return! start info 0
                        elif failedCount = 2 then
                            alarm fails
                            return! start info 0
                        else
                            return! start info (failedCount + 1)
                    | Some msg ->
                        match msg with
                        | Start info -> return! start info 0
                        | Stop -> return! stop ()
                        | Exit -> return ()
                        | Get replyChannel ->
                            replyChannel.Reply (Some info)
                            return! start info failedCount }

            and stop () =
                async {
                    log "stop ..."
                    let! msg = inbox.Receive ()
                    match msg with
                    | Start info -> return! start info 0
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
