#load "Bootstrap.fsx"
#load "typedef.fsx"
#load "database.fsx"
#load "engine.fsx"

open System
open System.Net

open Akka.Actor
open Akka.FSharp

open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

open Typedef
open Engine

let clientNode eng wsoc (mailbox:Actor<_>) = 
    let rec loop () =
        actor{
            let! message = mailbox.Receive()
            match message with
            | _ -> printfn "message received"
            return! loop ()
        }
    loop()


Engine.twitsetdb(10)
let twitterEngine = spawn rs "twitter-engine" engine

let ws (webSocket : WebSocket) (context: HttpContext) =
    socket {
        let mutable loop = true
        let mutable u = User()
        let nodeName = "cnode" + string(webSocket.GetHashCode())
        let node = spawn rs nodeName <| clientNode twitterEngine webSocket

        while loop do
            let! msg = webSocket.read()
            
            match msg with
            | (Text, data, true) ->
                let str = UTF8.toString data
                printfn "%A" str
                let response = sprintf "response to %s" str
                let byteResponse =
                    response
                    |> System.Text.Encoding.ASCII.GetBytes
                    |> ByteSegment
                do! webSocket.send Text byteResponse true

            | (Close, _, _) ->
                let emptyResponse = [||] |> ByteSegment
                do! webSocket.send Close emptyResponse true
                loop <- false

            | _ -> ()
    }

let app : WebPart =
    choose [
      path "/websocket" >=> handShake ws
      GET >=> choose [ path "/" >=> file "index.html"; browseHome ]
      NOT_FOUND "Found no handlers." ]

startWebServer defaultConfig app