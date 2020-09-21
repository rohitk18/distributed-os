open System
open Akka.Actor
open Akka.FSharp

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


[<EntryPoint>]
let main argv =
    printfn "Hello World from F#!"

// defined unsigned int to input numbers since only positive integers are being involved
    let n:int = int argv.[0]
    let k:int = int argv.[1]
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

    let masterActor calculators (mailbox:Actor<_>) = 
        let rec loop() = actor {
            let! message = mailbox.Receive()
            match box message with
            // | :? string as msg -> printfn "ScalarValuePrinter: received String %s" msg
            | :? string as msg -> 
                // for each calculator send message "Calculate" executes calculator actors
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
    // let maCalculators = spawn rootSystem "ma-calculators" <| calculator mareciever n
    let maCalculators = 
        [1 .. n]
        |> List.map(fun id ->   
                        let sid = "ma-calc-%A" id 
                        spawn rootSystem sid  <| calculator mareceiver id)
    
    let maRef = spawn rootSystem "master-actor" <| masterActor maCalculators
    
    maRef <! "Knock knock!"
    printfn "End of code"
    Threading.Thread.Sleep 5000
    0 // return an integer exit code
