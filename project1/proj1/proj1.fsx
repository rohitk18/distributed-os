#time "on"
#load "Bootstrap.fsx"

open System
open Akka.Actor
open Akka.FSharp

type MessageObject = CommandObject of startnum:bigint * size:bigint

let sumOfSquares (n:bigint):bigint =
    (* Sum of squares of positive integers from 1 to n (including) *)
    if n.IsZero then 0I
    else n * (n+1I) * (2I*n + 1I) / 6I

let checkSquare (n:bigint):bool =
    (* Sum of squares of positive integers from 1 to n (including) *)
    let sqroot=sqrt (double n)
    let number = truncate sqroot
    number*number = (double)n 

(*Returns boolean value if the checked sum is a square*)
let sumOfSquaresRange (n:bigint,k:bigint):bool = 
    (* Sum of squares of integers from n to n+k-1 *)
    if n.IsZero then false
    elif n.IsOne then k |> sumOfSquares |> checkSquare
    else sumOfSquares(n+k-1I) - sumOfSquares(n-1I) |> checkSquare

// Input
let n:bigint = System.Numerics.BigInteger.Parse fsi.CommandLineArgs.[1]
let k:bigint = System.Numerics.BigInteger.Parse fsi.CommandLineArgs.[2]

let size:bigint = n/8I + 1I

(* Calculator Actor model*)
let calculator (mailbox:Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | CommandObject(num,s) -> 
            let mutable l:bigint list = []
            for i in [num .. num+s-1I] do
                let b = sumOfSquaresRange(i,k)
                match b with
                | true -> l <- i :: l
                | false -> ()
                l <- List.rev l
            mailbox.Sender() <!  l
        // | _-> failwith "Improper Calculator Command"
        return! loop ()
    }
    loop ()

let rootSystem = ActorSystem.Create("Proj1")

let calcs = 
        [1I .. size .. n]
        |> List.map(fun id ->   
            let sid = string id
            let cs = "ma-calc-" + sid
            spawn rootSystem cs calculator) // Calculator Actors

// Print output
let mutable reqs:Async<bigint list> list = []
let mutable response:bigint list = []
for i in 0..calcs.Length-1 do
    reqs <- (calcs.[i] <? CommandObject((bigint i)*size+1I,size)) :: reqs

for request in reqs do
    response <- Async.RunSynchronously request
    for i in response do
        printfn "%A" i