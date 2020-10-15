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



[<EntryPoint>]
let main argv =
    // printfn "Hello World from F#!"
    let numActors:int = argv.[0] |> int
    let topology:string = argv.[1]
    let algorithm:string = argv.[2]

    let system:ActorSystem = ActorSystem.Create("AsyncGossip")

    let allActors:List<IActorRef> = new List<IActorRef>()
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

    let messagePropSuccess = 
        messageCounter <- messageCounter + 1
        if messageCounter = numActors then
            printfn "Message has propagated through entire system, nodes reached = %d" messageCounter

    let buildNeighboursForNode(nodeIndex:int,topology:string) =
        let neighbourActors = new List<IActorRef>()
        match topology with
        | "line" ->
            if nodeIndex - 1 >= 0 then
                neighbourActors.Add(allActors.[nodeIndex-1])
            if nodeIndex + 1 < numActors then
                neighbourActors.Add(allActors.[nodeIndex+1])
            let request = allActors.[nodeIndex] <? BuildNeighbours(neighbourActors)
            let response = Async.RunSynchronously request
            ()
        | "full" -> 
            // let tempActors = new List<IActorRef>()
            neighbourActors.AddRange(allActors)
            neighbourActors.Remove(allActors.[nodeIndex]) |> ignore
            let request = allActors.[nodeIndex] <? BuildNeighbours(neighbourActors)
            let response = Async.RunSynchronously request
            // printfn "%A" response
            ()
        | "2D" -> 
            if nodeIndex - numActorssqrt >= 0 then neighbourActors.Add(allActors.[nodeIndex- numActorssqrt])
            if nodeIndex + numActorssqrt < numActors then neighbourActors.Add(allActors.[nodeIndex+numActorssqrt])
            if nodeIndex - 1 >= 0 && (nodeIndex - 1)/numActorssqrt = nodeIndex / numActorssqrt then neighbourActors.Add(allActors.[nodeIndex-1])
            if nodeIndex + 1 < numActors && (nodeIndex + 1)/numActorssqrt = nodeIndex / numActorssqrt then neighbourActors.Add(allActors.[nodeIndex+1])
            let request = allActors.[nodeIndex] <? BuildNeighbours(neighbourActors)
            let response = Async.RunSynchronously request
            // printfn "%A" response
            ()
        | "imp2D" ->
            if nodeIndex - numActorssqrt >= 0 then neighbourActors.Add(allActors.[nodeIndex- numActorssqrt])
            if nodeIndex + numActorssqrt < numActors then neighbourActors.Add(allActors.[nodeIndex+numActorssqrt])
            if nodeIndex - 1 >= 0 && (nodeIndex - 1)/numActorssqrt = nodeIndex / numActorssqrt then neighbourActors.Add(allActors.[nodeIndex-1])
            if nodeIndex + 1 < numActors && (nodeIndex + 1)/numActorssqrt = nodeIndex / numActorssqrt then neighbourActors.Add(allActors.[nodeIndex+1])
            let rand = Random()
            let mutable genRand:int = rand.Next(numActors)
            while genRand = nodeIndex - numActorssqrt || genRand = nodeIndex + numActorssqrt || genRand = nodeIndex - 1 || genRand = nodeIndex + 1 do
                genRand <- rand.Next(numActors)
            neighbourActors.Add(allActors.[genRand])
            let request = allActors.[nodeIndex] <? BuildNeighbours(neighbourActors)
            let response = Async.RunSynchronously request
            // printfn "%A" response
            ()
        | _ -> printfn "Unknown topology"

    let nodeTerminated(worker:IActorRef) =
        terminatedNodeCount <- terminatedNodeCount + 1
        // printfn "%A" worker
        if terminatedNodeCount = numActors then
            printfn "-------------------------------------"
            let timeDiff = msgPropTime.ElapsedMilliseconds
            printfn "Covergence Time for message is %d" msgPropTime.ElapsedMilliseconds
            // printfn "Convergence Time for message is " + System.currentTimeMillis() - msgPropTime
            printfn "-------------------------------------"
        elif algorithm.Equals("push-sum") && terminatedNodeCount = 1 then 
            printfn "Covergence Time for message is %d" msgPropTime.ElapsedMilliseconds
            // println("Convergence Time for message is " + (System.currentTimeMillis() - msgPropTime))
            printfn "-------------------------------------"
            // context.system.shutdown() 
        if topology.Equals("imp2D") then
            if aliveActors.Count> 0 then
                let sender = worker
                aliveActors.Remove(sender) |> ignore
                for i in 0 .. aliveActors.Count-1 do
                    aliveActors.[i] <! Discard(sender)

    let destruct (worker:IActorRef,neighbours:List<IActorRef>)=
        // terminated <- true
        // masterRef <! NodeTerminated
        nodeTerminated(worker)
        if neighbours.Count>0 then
            for i in 0 .. neighbours.Count-1 do
                let request = neighbours.[i] <? NeighbourTerminate
                let response = Async.RunSynchronously request
                printfn response
        // mailbox.Context.Stop(mailbox.Self)

    let worker (nodeIndex: int) (w:float) (mailbox:Actor<_>) = 
        let neighbours:List<IActorRef> = List<IActorRef> ()
        let mutable terminated:bool = false
        let mutable firstMessage:bool = true
        let mutable messageCounter:int = 0
        let mutable sLocal:float = float(nodeIndex+1)
        let mutable wLocal:float = w
        let mutable swCurrent:float = 0.0
        let mutable swPrevious:float = 0.0
        let mutable pushSumCounter:int = 0

        // Used to calculate the convergence for Push sum and defines the exit criterion
        let calculateConvergence =
            if (Math.Abs(swCurrent-swPrevious)<=0.0000000001) then
                pushSumCounter <- pushSumCounter + 1
                if pushSumCounter>=3 then 
                    terminated <- true
                    // destruct(mailbox.Self, neighbours)
            else
                pushSumCounter <- 0

        let rec loop() = actor {
            let! message = mailbox.Receive()

            match message with
            | BuildNeighbours(neighbourActors)->
                neighbours.AddRange(neighbourActors)
                mailbox.Sender() <! true
            | ImplementGossip ->
                if (not terminated && neighbours.Count>=0) then
                    printfn "%A" neighbours
                    printfn "%A" mailbox.Self
                    if firstMessage then
                        firstMessage <- false
                        // masterRef <! MessagePropSuccess
                        messagePropSuccess
                    messageCounter <- messageCounter + 1
                    printfn "%A %A" messageCounter mailbox.Self
                    if(messageCounter >=10 || neighbours.Count=0) then
                        terminated <- true
                        destruct(mailbox.Self,neighbours)
                    mailbox.Self <! SpreadGossip
            (*
            | ImplementPushSum(s,w) ->
                sLocal <- sLocal + s
                wLocal <- wLocal + w
                swCurrent <- sLocal/wLocal
                calculateConvergence
                if not terminated then
                    swPrevious <- swCurrent
                    mailbox.Self <! SpreadPushSum
            *)
            (*
            |SpreadPushSum->
                sLocal <- sLocal/2.0
                wLocal <- wLocal/2.0
                if (not terminated && neighbours.Count > 0) then
                    let rnd = Random();
                    neighbours.[rnd.Next(0,neighbours.Count)] <! ImplementPushSum(sLocal,wLocal)
                    mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan(0,0,0,50),mailbox.Self,SpreadPushSum)
            *)
            |SpreadGossip->
                if (not terminated && neighbours.Count > 0) then
                    printfn "Spread gossip working %A" mailbox.Self
                    let rnd = Random();
                    neighbours.[rnd.Next(0,neighbours.Count)] <! ImplementGossip
                    mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan(0,0,0,50),mailbox.Self,SpreadGossip)
                else 
                    terminated <-true
                    destruct(mailbox.Self,neighbours)
            |NeighbourTerminate ->
                printfn "runns"
                if (not terminated && neighbours.Count > 0) then
                    let sender = mailbox.Sender()
                    if (neighbours.Contains(sender)) then neighbours.Remove(sender) |> ignore
                    // handle else also
                    sender <! true
                    if neighbours.Count = 0 then
                        terminated <- true
                        destruct(mailbox.Self, neighbours)
            (*
            | Discard(neighbour)->    
                printfn "discard runs"
                if (not terminated && neighbours.Count > 0) then
                    neighbours.Remove(neighbour) |> ignore
                    if (neighbours.Count = 0) then destruct
                    *)
            | _->printfn "Undefined Message"
            return! loop()
        }
        loop()

    for i in 0 .. numActors-1 do
        let sid = string i
        let name = "worker-"+sid
        let workerActor = spawn system name <| worker i 1.0
        allActors.Add(workerActor)
        aliveActors.Add(workerActor)

    printfn "Start Gossip"
    msgPropTime.Start()
    for i in 0 .. numActors-1 do buildNeighboursForNode(i,topology) |> ignore
   
    match algorithm with
    | "gossip" ->
        let rand = Random()
        allActors.[rand.Next(0,numActors)] <! ImplementGossip
        // allActors.[rand.Next(0,numActors)] <! PrintNeighbours
    | "push-sum" ->
        printfn "Implement Push sum"
        // let rand = Random()
        // allActors.[rand.Next(0,numActors)] <! ImplementPushSum(1.0, 0.0)
    | _ -> printfn "Undefined Algorithm"

    Threading.Thread.Sleep 10000
    0 // return an integer exit code
