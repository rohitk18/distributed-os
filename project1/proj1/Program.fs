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
    // printf "test: %A\n" n
    let sqroot=sqrt (double n)
    let number = truncate sqroot
    number*number = (double)n 

let sumOfSquaresRange (n:bigint,k:bigint):bool = 
    (* Sum of squares of integers from n to n+k-1 *)
    if n.IsZero then false
    elif n.IsOne then k |> sumOfSquares |> checkSquare
    else sumOfSquares(n+k-1I) - sumOfSquares(n-1I) |> checkSquare

[<EntryPoint>]
let main argv =
    // get arguments from command line as big integers
    let n:bigint = System.Numerics.BigInteger.Parse(argv.[0])
    let k:bigint = System.Numerics.BigInteger.Parse(argv.[1])
    let mutable size:bigint = n/8I + 1I

    let calculator (mailbox:Actor<_>) =
        let rec loop () = actor {
            let! message = mailbox.Receive ()
            match message with
            | CommandObject(num,s) -> 
                // printf "Calculating %A %A...Done\n" num s
                let mutable l = []
                for i in [num .. num+s-1I] do
                    let b = sumOfSquaresRange(i,k)
                    match b with
                    | true -> l <- float i :: l
                    | false -> ()
                l <- List.rev l
                mailbox.Sender() <! l
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
                spawn rootSystem cs calculator)
    
    let mutable reqs:Async<bigint list> list = []
    for i in 0..calcs.Length-1 do
        reqs <- (calcs.[i] <? CommandObject((bigint i)*size+1I,size)) :: reqs
    
    let request = Async.RunSynchronously reqs.[0]
    for i in request do
        printfn "%A" i
        
    
    printfn "End of code"
    // Threading.Thread.Sleep 1000
    0 // return an integer exit code
