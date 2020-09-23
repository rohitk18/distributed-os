#time "on"
#load "Bootstrap.fsx"

open System
open Akka.Actor
open Akka.FSharp

// type Bint = Numerics.BigInteger

for arg in fsi.CommandLineArgs do
    printfn "%s" arg

let n:bigint = bigint fsi.CommandLineArgs.[1]
let k:bigint = bigint fsi.CommandLineArgs.[2]
