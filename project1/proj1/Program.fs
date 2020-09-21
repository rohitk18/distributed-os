open System
open Akka.Actor
open Akka.FSharp

type Bint = Numerics.BigInteger

let sumOfSquares (n:int):int =
    (* Sum of squares of positive integers from 1 to n (including) *)
    match n with
    | 0 -> 0
    | _ -> n * (n+1) * (2*n + 1) / 6

let sumOfSquaresRange (n:int,k:int):int = 
    (* Sum of squares of integers from n to n+k-1 *)
    match k with
    | 0 -> 0
    | 1 -> sumOfSquares n
    | _ -> sumOfSquares(n+k-1) - sumOfSquares(n-1)

// let checkPerfectSquare (n:bigint):bool =
//     (* Sum of squares of positive integers from 1 to n (including) *)
//     let sqroot=sqrt (float n)
//     let number = truncate sqroot
//     number*number= (float)n 


[<EntryPoint>]
let main argv =
    // get arguments from command line as big integers
    let n = int argv.[0]
    let k = int argv.[1]
    Console.WriteLine("{0}",sumOfSquaresRange(n,k))

    let rootSystem = ActorSystem.Create("Proj1")
    (*
    // let masterActor = 
    //     spawn rootSystem "master-actor"
    //     <| fun mailbox ->
    //         actor {
    //             let! message = mailbox.Receive()
    //             match box message with
    //             | :? string as msg -> printfn "Hello %s" msg
    //             | _ ->  failwith "unknown message"
    //         } 
    
    // masterActor <! "F#!"
    *)

    let masterActor (calculators:List<_>) (mailbox:Actor<_>) = 
        let rec loop() = actor {
            let! message = mailbox.Receive()
            match box message with
            // | :? string as msg -> printfn "ScalarValuePrinter: received String %s" msg
            | :? string as msg -> 
                // for each calculator send message "Calculate" executes calculator actors
                printfn "Message at master: %s" msg
                calculators
                |> List.map(fun calc -> 
                                calc <! msg)
            | :? int as msg -> printfn "ScalarValuePrinter: received Int %i" msg
            | _ -> ()
            return! loop()
        }
        loop()

    let calculator receiverActor num (mailbox:Actor<_>) =
        let rec loop ractor n = actor {
            let! message = mailbox.Receive()
            match box message with
            | :? string as msg -> 
                printfn "Message:%s Calculating %i...Done" msg n
                ractor <! msg
            | _->()
            return! loop ractor n
        }
        loop receiverActor num
    
    let receiver num (mailbox:Actor<_>) =
        let rec loop n = actor{
            let! message = mailbox.Receive()
            match box message with
            | :? string as msg -> printfn "Message at Receiver: %s %i" msg n
            | _->()
            return! loop n
        }
        loop num

    // let maRef = spawn rootSystem "master-actor" masterActor
    let maReceiver = spawn rootSystem "receiver-actor" <| receiver n


    // let maCalculators = spawn rootSystem "ma-calculators" <| calculator maRef n
    // let maCalculators = spawn rootSystem "ma-calculators" <| calculator maReceiver n
    // let mac1 = spawn rootSystem "ma-calculators-1" <| calculator maReceiver 1
    // let mac2 = spawn rootSystem "ma-calculators-2" <| calculator maReceiver 2
    let maCalculators = 
        [1 .. n]
        |> List.map(fun id ->   
                        // printfn "%i" id
                        let sid = string id
                        let cs = "ma-calc-" + sid
                        // printfn "%s" cs )
                        spawn rootSystem cs  <| calculator maReceiver id)
    
    // let maRef = spawn rootSystem "master-actor" <| masterActor [mac1,mac2]
    let maRef = spawn rootSystem "master-actor" <| masterActor maCalculators
    
    maRef <! "Knock knock!"
    printfn "End of code"
    Threading.Thread.Sleep 5000
    0 // return an integer exit code
