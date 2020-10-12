// Learn more about F# at http://fsharp.org

open System
open System.Collections.Generic
open Akka.FSharp
open Akka.Actor

type Message =
    | BuildNeighbours of neighbourActors:List<IActorRef>
    | ImplementGossip
    | ImplementPushSum of s:float * w:float
    | NodeTerminated
    | MessagePropSuccess
    | NeighbourTerminate
    | SpreadPushSum
    | PrintNeighbours
    | SpreadGossip
    | StartGossip
    | BuildForPushSum of s:float * w:float
    | Discard of neighbour:IActorRef

let worker (masterRef:IActorRef) (mailbox:Actor<_>) = 
    let rec loop() = actor {
        let neighbours:List<IActorRef> = new List<IActorRef>();
        let mutable terminated:bool = false
        let mutable firstMessage:bool = true
        let mutable messageCounter:int = 0
        let mutable sLocal:float = 0.0
        let mutable wLocal:float = 0.0
        let mutable swCurrent:float = 0.0
        let mutable swPrevious:float = 0.0
        let mutable pushSumCounter:int = 0
        let! message = mailbox.Receive()

        // Destruct is invoked if the exit criterion for the Actor is met, it informs the Master and Neighbours that it has exited
        let destruct =
            terminated <- true
            masterRef <! NodeTerminated
            if neighbours.Count>0 then
                for i in 0 .. neighbours.Count-1 do
                    neighbours.[i] <! NeighbourTerminate
            mailbox.Context.Stop(mailbox.Self)            
        
        // Used to calculate the convergence for Push sum and defines the exit criterion
        let calculateConvergence =
            if (Math.Abs(swCurrent-swPrevious)<=0.0000000001) then
                pushSumCounter <- pushSumCounter + 1
                if pushSumCounter>=3 then destruct
            else
                pushSumCounter <- 0
        
        match message with
        | BuildNeighbours(neighbourActors)->
            neighbours.AddRange(neighbourActors)
        | PrintNeighbours ->
            let sb:Text.StringBuilder = Text.StringBuilder()
            sb.Append("Neighbours of "+ mailbox.Self.ToString() + " are ") |> ignore
            for i in 0 .. neighbours.Count-1 do
                sb.Append(neighbours.[i].ToString() + " ") |> ignore
        | ImplementGossip ->
            if (not terminated && neighbours.Count>=0) then 
                if firstMessage then
                    firstMessage <- false
                    masterRef <! MessagePropSuccess
                messageCounter <- messageCounter + 1
                if(messageCounter >=10 || neighbours.Count=0) then
                    terminated <- true
                    destruct
                mailbox.Self <! SpreadGossip
        | ImplementPushSum(s,w) ->
            sLocal <- sLocal + s
            wLocal <- wLocal + w
            swCurrent <- sLocal/wLocal
            calculateConvergence
            if not terminated then
                swPrevious <- swCurrent
                mailbox.Self <! SpreadPushSum
        |SpreadPushSum->
            sLocal <- sLocal/2.0
            wLocal <- wLocal/2.0
            if (not terminated && neighbours.Count > 0) then
                let rnd = Random();
                neighbours.[rnd.Next(0,neighbours.Count)] <! ImplementPushSum(sLocal,wLocal)
                mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan(0,0,0,50),mailbox.Self,SpreadPushSum)
        |SpreadGossip->
            if (not terminated && neighbours.Count > 0) then
                let rnd = Random();
                neighbours.[rnd.Next(0,neighbours.Count)] <! ImplementGossip
                mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan(0,0,0,50),mailbox.Self,SpreadGossip)
            else destruct
        |NeighbourTerminate ->
            if (not terminated && neighbours.Count > 0) then
                let sender = mailbox.Sender()
                if (neighbours.Contains(sender)) then neighbours.Remove(sender) |> ignore
                // handle else also
                if neighbours.Count = 0 then destruct
        | BuildForPushSum(s,w)->
            sLocal <- s
            wLocal <- w
        | Discard(neighbour)->    
            if (not terminated && neighbours.Count > 0) then
                neighbours.Remove(neighbour) |> ignore
                if (neighbours.Count = 0) then destruct
        | _->printfn "Undefined Message"
        return! loop()
    }
    loop()

let master (rootSystem:ActorSystem) (numActors:int) (topology:string) (algorithm:string) (mailbox:Actor<_>) =
    let rec loop() = actor {
        let allActors:List<IActorRef> = new List<IActorRef>()
        let neighbourActors:List<IActorRef> = new List<IActorRef>()
        let aliveActors:List<IActorRef> = new List<IActorRef>()
        let mutable terminatedNodeCount:int = 0
        let mutable messageCounter:int = 0
        let numActorssqrt:int = Math.Ceiling(Math.Sqrt(numActors |> float)) |> int
        let mutable numActors = numActors
        match topology with
        | "2D" | "imp2D" -> numActors <- numActorssqrt * numActorssqrt
        | _ -> ()
        let msgPropTime:Diagnostics.Stopwatch =  Diagnostics.Stopwatch()
        msgPropTime.Reset()
        
        let! message = mailbox.Receive()

        let buildNeighboursForNode(nodeIndex:int,topology:string) =
            match topology with
            | "line" ->
                if nodeIndex - 1 >= 0 then
                    neighbourActors.Add(allActors.[nodeIndex-1])
                    if nodeIndex < numActors - 1 then
                        neighbourActors.Add(allActors.[nodeIndex+1])
                allActors.[nodeIndex] <! BuildNeighbours(neighbourActors)
            | "full" -> 
                let tempActors = new List<IActorRef>()
                tempActors.AddRange(allActors)
                tempActors.Remove(allActors.[nodeIndex]) |> ignore
                allActors.[nodeIndex] <! BuildNeighbours(tempActors)
            | "2D" -> 
                if nodeIndex - numActorssqrt >= 0 then neighbourActors.Add(allActors.[nodeIndex- numActorssqrt])
                if nodeIndex + numActorssqrt < numActors then neighbourActors.Add(allActors.[nodeIndex+numActorssqrt])
                if nodeIndex - 1 >= 0 then neighbourActors.Add(allActors.[nodeIndex-1])
                if nodeIndex + 1 < numActors then neighbourActors.Add(allActors.[nodeIndex+1])
            | "imp2D" ->
                if nodeIndex - numActorssqrt >= 0 then neighbourActors.Add(allActors.[nodeIndex- numActorssqrt])
                if nodeIndex + numActorssqrt < numActors then neighbourActors.Add(allActors.[nodeIndex+numActorssqrt])
                if nodeIndex - 1 >= 0 then neighbourActors.Add(allActors.[nodeIndex-1])
                if nodeIndex + 1 < numActors then neighbourActors.Add(allActors.[nodeIndex+1])
                let rand = Random()
                let mutable genRand:int = rand.Next(numActors)
                while genRand = nodeIndex - numActorssqrt || genRand= nodeIndex + numActorssqrt || genRand = nodeIndex - 1 || genRand = nodeIndex + 1 do
                    genRand <- rand.Next(numActors)
                neighbourActors.Add(allActors.[genRand])
            | _ -> printfn "Unknown topology"
            neighbourActors = new List<IActorRef>()

        for i in 0 .. numActors-1 do
            let sid = string i
            let name = "worker-"+sid
            let workerActor = spawn rootSystem name <| worker mailbox.Self
            allActors.Add(workerActor)
            aliveActors.Add(workerActor)

        match message with
        | StartGossip ->
            printfn "Start Gossip"
            msgPropTime.Start()
            for i in 0 .. numActors-1 do
                buildNeighboursForNode(i,topology) |> ignore
                allActors.[i] <! BuildForPushSum(float(i+1),1.0)
            match algorithm with
            | "gossip" ->
                printfn "Implement Gossip"
                let rand = Random()
                allActors.[rand.Next(0,numActors)] <! ImplementGossip
            | "push-sum" ->
                printfn "Implement PUsh sum"
                let rand = Random()
                allActors.[rand.Next(0,numActors)] <! ImplementPushSum(1.0, 0.0)
            | _ -> printfn "Undefined Algorithm"
        | NodeTerminated->
            terminatedNodeCount <- terminatedNodeCount + 1
            if terminatedNodeCount = numActors then
                printfn "-------------------------------------"
                let timeDiff = msgPropTime.ElapsedMilliseconds
                printfn "Covergence Time for message is %d" msgPropTime.ElapsedMilliseconds
                // printfn "Convergence Time for message is " + System.currentTimeMillis() - msgPropTime
                printfn "-------------------------------------"
                mailbox.Context.System.Terminate
            elif algorithm.Equals("push-sum") && terminatedNodeCount = 1 then 
                printfn "Covergence Time for message is %d" msgPropTime.ElapsedMilliseconds
                // println("Convergence Time for message is " + (System.currentTimeMillis() - msgPropTime))
                printfn "-------------------------------------"
                // context.system.shutdown() 
                mailbox.Context.System.Terminate
            if topology.Equals("imp2D") then
                if aliveActors.Count> 0 then
                    let sender = mailbox.Sender()
                    aliveActors.Remove(sender) |> ignore
                    for i in 0 .. aliveActors.Count-1 do
                        aliveActors.[i] <! Discard(sender)                  
        | MessagePropSuccess->
            messageCounter <- messageCounter + 1
            if messageCounter = numActors then
                printfn "Message has propagated through entire system, nodes reached = %d" messageCounter
        | _ -> printfn "Can't understand"
        return! loop()
    }
    loop()

[<EntryPoint>]
let main argv =
    // printfn "Hello World from F#!"
    let numActors:int = argv.[0] |> int
    let topology:string = argv.[1]
    let algorithm:string = argv.[2]
    // printfn "%d" numActors
    // printfn "%s" topology
    // printfn "%s" algorithm
    
    let system:ActorSystem = ActorSystem.Create("AsyncGossip")
    let masterObj:IActorRef = spawn system "master" <| master system numActors topology algorithm
    // let workerActors:List<IActorRef> = [for i in 1 .. numActors ->
    //                                         let sid = string i
    //                                         let name = "worker-" + sid
    //                                         spawn system name <| worker masterObj
    //                                     ]
    masterObj <! StartGossip
    Threading.Thread.Sleep 1000
    0 // return an integer exit code
