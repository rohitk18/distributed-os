#load "Bootstrap.fsx"
#load "engine.fsx"

open System
open Akka.Actor
open Akka.FSharp
open Engine

let mutable serverStop = true

let server = spawn Engine.rs "server" engine

printfn "%A" server

while serverStop do
    Threading.Thread.Sleep 1000