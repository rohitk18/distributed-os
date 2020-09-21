// #I "/home/rohitk18/.nuget/packages"
#r "nuget: Akka.dll"
// #r "Akka.FSharp.dll"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let system = ActorSystem.Create("FSharp")

// type EchoServer =
//     inherit Actor

//     override x.OnReceive message =
//         match message with
//         | :? string as msg -> printfn "Hello %s" msg
//         | _ ->  failwith "unknown message"

// let echoServer = system.ActorOf(Props(typedefof<EchoServer>, Array.empty))

// echoServer <! "F#!"