open System
open Akka.Actor
open Akka.FSharp

type ResultObject = ResultObject of isquare:bool * startnum:bigint

let sumOfSquares (n:bigint):bigint =
    (* Sum of squares of positive integers from 1 to n (including) *)
    if n.IsZero then 0I
    else n * (n+1I) * (2I*n + 1I) / 6I

let checkSquare (n:bigint) =
    (* Sum of squares of positive integers from 1 to n (including) *)
    // printf "test: %A\n" n
    let sqroot=sqrt (double n)
    let number = truncate sqroot
    number*number = (double)n 

let sumOfSquaresRange (n:bigint,k:bigint):ResultObject = 
    (* Sum of squares of integers from n to n+k-1 *)
    if n.IsZero then ResultObject(false,0I)
    elif n.IsOne then ResultObject(k |> sumOfSquares |> checkSquare,1I)
    else ResultObject(sumOfSquares(n+k-1I) - sumOfSquares(n-1I) |> checkSquare,n)

[<EntryPoint>]
let main argv =
    // get arguments from command line as big integers
    let n:bigint = System.Numerics.BigInteger.Parse(argv.[0])
    let k:bigint = System.Numerics.BigInteger.Parse(argv.[1])
    // Console.WriteLine("{0}",sumOfSquaresRange(n,k))

    let rootSystem = ActorSystem.Create("Proj1")

    let masterActor (calculators:List<_>) (mailbox:Actor<_>) = 
        let rec loop() = actor {
            let! message = mailbox.Receive()
            match message with
            | "Start" -> 
                // for each calculator send message "Calculate" executes calculator actors
                // calculators
                // |> List.map(fun calc -> 
                //                 calc <! "Calculate")
                for calc in calculators do
                    calc <! "Calculate"
            | _ -> failwith "Improper Command"
            return! loop()
        }
        loop()

    let calculator receiverActor (num:bigint) (mailbox:Actor<_>) =
        let rec loop ractor no = actor {
            let! message = mailbox.Receive()
            match message with
            | "Calculate" -> 
                // printfn "Calculating %A...Done" no
                ractor <! sumOfSquaresRange(no, k)
            | _-> failwith "Improper Calculator Command"
            return! loop ractor no
        }
        loop receiverActor num
    
    let receiver (num:bigint) (mailbox:Actor<_>) =
        let mutable x = num
        let rec loop() = actor{
            let! ResultObject(b,s) = mailbox.Receive()
            if x > 0I then
                match b with
                | true -> printfn "%A" s
                | false -> ()
                x <- x - 1I
            else
                failwith "Result Overload"
            return! loop()
        }
        loop ()

    let maReceiver = spawn rootSystem "receiver-actor" <| receiver n

    let maCalculators = 
        [1I .. n]
        |> List.map(fun id ->   
                        let sid = string id
                        let cs = "ma-calc-" + sid
                        spawn rootSystem cs  <| calculator maReceiver id)
    
    let maRef = spawn rootSystem "master-actor" <| masterActor maCalculators
    
    maRef <! "Start"
    printfn "End of code"
    Threading.Thread.Sleep 5000
    0 // return an integer exit code
