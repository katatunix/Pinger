namespace Pinger

open System
open System.Net
open System.Net.Sockets

module Core =

    type Result<'A, 'B> =
        | Ok of 'A
        | Error of 'B

    type Server = {
        Address : string
        Port : int }

    type MonitorInfo = {
        Servers : Server list
        Interval : int }

    let connect (server : Server) =
        let TIMEOUT_SEC = float 10
        try
            use client = new TcpClient ()
            let result =
                match IPAddress.TryParse server.Address with
                | true, ip ->
                    client.BeginConnect (ip, server.Port, null, null)
                | _ ->
                    client.BeginConnect (server.Address, server.Port, null, null)
            let notTimeout = result.AsyncWaitHandle.WaitOne (TimeSpan.FromSeconds TIMEOUT_SEC)
            notTimeout && client.Connected
        with _ ->
            false

    let getFailedServers (servers : Server list) =
        servers |> List.filter (fun server -> not (connect server))

    type private Msg =
        | Start of MonitorInfo
        | Stop
        | Exit
        | Get of AsyncReplyChannel<MonitorInfo option>

    let private mailBox alarm (inbox : MailboxProcessor<Msg>) =
        let rec active (info : MonitorInfo) =
            async {
                let! msgOp = inbox.TryReceive (info.Interval * 60000)
                match msgOp with
                | None ->
                    let getFails () = getFailedServers info.Servers
                    let fails =
                        [ 1..2 ]

                        |> List.fold
                            (fun fails _ -> if fails |> List.isEmpty then fails else getFails ())
                            (getFails ())
                    
                    if not fails.IsEmpty then
                        alarm fails
                    return! active info

                | Some msg ->
                    return! handleMsg msg (fun replyChannel -> replyChannel.Reply (Some info); active info) }

        and idle () =
            async {
                let! msg = inbox.Receive ()
                return! handleMsg msg (fun replyChannel -> replyChannel.Reply None; idle ()) }

        and handleMsg msg (replyHandler : AsyncReplyChannel<MonitorInfo option> -> Async<unit>) =
            async {
                match msg with
                | Start info -> return! active info
                | Stop -> return! idle ()
                | Exit -> return ()
                | Get replyChannel -> return! replyHandler replyChannel }

        idle ()

    [<AllowNullLiteral>]
    type Agent (alarm) =
        let mp = MailboxProcessor.Start (mailBox alarm)

        member this.Start info =
            mp.Post (Start info)

        member this.Stop () =
            mp.Post Stop

        member this.Exit () =
            mp.Post Exit

        member this.GetMonitorInfo () =
            mp.PostAndReply (fun replyChannel -> Get replyChannel)
