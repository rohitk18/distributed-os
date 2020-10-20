
#time "on"
#r @"bin/Debug/netcoreapp3.1/Akka.FSharp.dll"
#r @"bin/Debug/netcoreapp3.1/Akka.dll"
#r @"bin/Debug/netcoreapp3.1/Akka.TestKit.dll"

open System.Diagnostics
open System
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp


let args = fsi.CommandLineArgs.[1] 
let mutable numNodes = args |> int 
let topology = fsi.CommandLineArgs.[2]
let algorithm = fsi.CommandLineArgs.[3] 
let mutable fullConvergence  = true

type WorkerMessage = 
        struct
           [<DefaultValue>] val  mutable message: string
           [<DefaultValue>] val  mutable topology: string
           [<DefaultValue>] val  mutable neighbourWorkers: List<int>
           [<DefaultValue>] val  mutable algorithm: string 
           [<DefaultValue>] val  mutable workerNum: int
           [<DefaultValue>] val  mutable s: decimal
           [<DefaultValue>] val  mutable w: decimal
           [<DefaultValue>] val  mutable swRatio: float
           [<DefaultValue>] val  mutable state:String 

        end

let system = ActorSystem.Create("FSharp")       
let mutable gotMessage = Set.empty 
let mutable neighbourList = new List<IActorRef>()   
let mutable terminatedSet = Set.empty   

let timer = Stopwatch() 
timer.Start()


let prftSqr n =                 
    let h = n &&& 0xF
    if (h > 9) then false
    else
        if ( h <> 2 && h <> 3 && h <> 5 && h <> 6 && h <> 7 && h <> 8 ) then
            let t = ((n |> double |> sqrt) + 0.5) |> floor|> int
            t*t = n
        else false


if(topology = "2D" || topology = "imp2D") then
                let mutable x = prftSqr numNodes
                while (not x ) do
                        numNodes <- numNodes + 1
                        x <- prftSqr numNodes


let gossipWorker (mailbox: Actor<WorkerMessage>) =
    let mutable messageCounter =0           
    let mutable workerState = true       
    let mutable neighbourWorkers = null   
    let mutable workerNum = 0          
    let mutable convergedWorkers = Set.empty 
    let rec loop () = actor {
      
        let! message = mailbox.Receive()
        if(message.state = "init") then       
                workerNum <- message.workerNum
                neighbourWorkers <- message.neighbourWorkers
        elif (message.state <>"spread" && not workerState) then     
                let mutable spreadMessage = WorkerMessage()
                spreadMessage.state <- "spread"
                spreadMessage.workerNum <- workerNum
                mailbox.Sender() <! spreadMessage
        else
             if(message.state = "spread") then                
                        convergedWorkers <- convergedWorkers.Add(message.workerNum)
             
             if(messageCounter < 10 && (message.state <> "spread")) then  
                messageCounter <- messageCounter + 1 
             let topology = message.topology
             gotMessage <- gotMessage.Add(message.workerNum)
             if(messageCounter= 10) then   
                workerState <- false
                terminatedSet<-terminatedSet.Add(workerNum)

             let rand = Random()
             let i = rand.Next()%neighbourWorkers.Count
             let mutable randIndex = neighbourWorkers.[i]
             if(convergedWorkers.Count < neighbourWorkers.Count) then
                while( randIndex = workerNum || convergedWorkers.Contains(randIndex)) do
                               let rand = Random()
                               let i = rand.Next()%neighbourWorkers.Count
                               randIndex <- neighbourWorkers.[i]
                let neighbour = neighbourList.[randIndex]  
                let mutable rumourMessage = WorkerMessage()
                rumourMessage.topology <- topology
                rumourMessage.workerNum <- randIndex
                rumourMessage.state <- "Send"
                neighbour <! rumourMessage
             else               
                if(fullConvergence && terminatedSet.Count < numNodes-1) then
                        let leftNeighbours = numNodes - terminatedSet.Count
                        let rand = Random()
                        let mutable randomWorkerID = workerNum
                        while (randomWorkerID = workerNum || terminatedSet.Contains(randomWorkerID) ) do
                                        randomWorkerID <- rand.Next()%numNodes
                        let randomNeighbour = neighbourList.[randomWorkerID]
                        let mutable rumourMessage = WorkerMessage()
                        rumourMessage.topology <- topology
                        rumourMessage.workerNum <- randIndex
                        rumourMessage.state <- "Send"
                        randomNeighbour <! rumourMessage
                else

                        printfn "Convergence Time: %i ms" timer.ElapsedMilliseconds
                        printfn "Please press Enter"
             
        return! loop ()
    }
    loop ()


let pushSumWorker (mailbox: Actor<WorkerMessage>) =
    let mutable prevswRatio = 0.0M              
    let mutable s = 0.0M
    let mutable w = 1.0M
    let mutable workerState = true            
    let mutable neighbourWorkers = null               
    let mutable workerNum = 0                
    let mutable terminatedCounter = 0                                
    let mutable terminatedNodes = new List<int>() 
    let rec loop () = actor {
    
        let! message = mailbox.Receive()
        if(message.state = "init") then        
                workerNum <- message.workerNum
                s <- (decimal message.workerNum) + 1.0M
                neighbourWorkers <- message.neighbourWorkers
        elif (message.state <>"spread" && not workerState) then 
                let mutable spreadMessage = WorkerMessage()
                spreadMessage.state <- "spread"
                spreadMessage.workerNum <- workerNum
                spreadMessage.s <- s
                spreadMessage.w <- w
                mailbox.Sender() <! spreadMessage
        else
             if(message.state = "spread") then     
                        terminatedNodes.Add(message.workerNum)
                        s <- (decimal s)*(decimal 2)
                        w <- (decimal w)*(decimal 2) 
             if(message.state <> "spread") then  
                s <- (s + message.s)
                w <- (w + message.w)
                if( Math.Abs(prevswRatio-(s/w)) < 0.0000000001M) then
                        terminatedCounter <- terminatedCounter + 1
                else
                        terminatedCounter <- 0            
             let topology = message.topology
             gotMessage <- gotMessage.Add(workerNum)   
          
             if(terminatedCounter = 3) then 
                workerState <- false
                terminatedSet<-terminatedSet.Add(workerNum)
             let rand = Random()
             let i = rand.Next()%neighbourWorkers.Count
             let mutable randIndex = neighbourWorkers.[i]
             if(terminatedNodes.Count < neighbourWorkers.Count) then
                while( randIndex = workerNum || terminatedNodes.Contains(randIndex) ) do
                               let rand = Random()
                               let i = rand.Next()%neighbourWorkers.Count
                               randIndex <- neighbourWorkers.[i]
                let neighbour = neighbourList.[randIndex]       
                let mutable rumourMessage = WorkerMessage()
                rumourMessage.topology <- topology
                rumourMessage.workerNum <- randIndex
                rumourMessage.state <- "Send"
                rumourMessage.s <- ((decimal s)/(decimal 2))
                rumourMessage.w <- ((decimal w)/(decimal 2))
                prevswRatio <- (s/w)            
                s <- (decimal s/decimal 2)     
                w <- (decimal w)/(decimal 2)
                neighbour <! rumourMessage
             else                       
                if(fullConvergence && terminatedSet.Count < numNodes-1) then
                        let leftNeighbours = numNodes - terminatedSet.Count
                        let rand = Random()
                        let mutable randomWorkerID = rand.Next()%numNodes
                        while (randomWorkerID = workerNum || terminatedSet.Contains(randomWorkerID) ) do
                                        randomWorkerID <- rand.Next()%numNodes
                        let randomNeighbour = neighbourList.[randomWorkerID]
                        let mutable rumourMessage = WorkerMessage()
                        rumourMessage.topology <- topology
                        rumourMessage.workerNum <- randIndex
                        rumourMessage.s <- (decimal s/decimal 2) 
                        rumourMessage.w <- (decimal w/decimal 2)
                        prevswRatio <- (s/w)
                        s <- (decimal s/decimal 2)      
                        w <- (decimal w)/(decimal 2)
                        rumourMessage.state <- "Send"
                        randomNeighbour <! rumourMessage
                else

                        printfn "Convergence Time: %i ms" timer.ElapsedMilliseconds
                        printfn "Please press Enter"
                
             
                
        return! loop ()
    }
    loop ()

let gossipTime() =
        for i =0 to numNodes-1 do                      
                let mutable neighbourWorkers = new List<int>()
                let actorName = "node"+ string i
                let nodeActorRef =  spawn system actorName gossipWorker
                if(topology = "full") then            
                    for j = 0 to numNodes-1 do
                        if(j<> i) then
                                neighbourWorkers.Add(j)
                elif(topology = "line") then          
                    if(i <> 0) then           
                        neighbourWorkers.Add(i-1)
                    if(i <> numNodes-1) then    
                        neighbourWorkers.Add(i+1)
                elif(topology = "2D") then            
                        let squareroot = sqrt(float numNodes)
                        if(i % int squareroot <> 0) then 
                                neighbourWorkers.Add(i-1)
                        if((i+1) % int squareroot <> 0) then 
                                neighbourWorkers.Add(i+1)
                        if((i- int squareroot )>= 0) then        
                                neighbourWorkers.Add(i-int squareroot)
                        if((i+int squareroot) < numNodes ) then       
                                neighbourWorkers.Add(i+ int squareroot)
                elif(topology = "imp2D") then         
                        let squareroot = sqrt(float numNodes)
                        if(i % int squareroot <> 0) then 
                                neighbourWorkers.Add(i-1)
                        if((i+1) % int squareroot <> 0) then 
                                neighbourWorkers.Add(i+1)
                        if((i- int squareroot )>= 0) then       
                                neighbourWorkers.Add(i-int squareroot)
                        if((i+int squareroot) < numNodes ) then     
                                neighbourWorkers.Add(i+ int squareroot)
                        let rand = Random()
                        let mutable randIndex = neighbourWorkers.[0]
                        while (neighbourWorkers.Contains(randIndex) && randIndex <> i) do 
                                randIndex <- rand.Next()%numNodes
                        neighbourWorkers.Add(randIndex)       
                let mutable initmessage = WorkerMessage()
                initmessage.state <- "init"        
                initmessage.neighbourWorkers<- neighbourWorkers
                initmessage.workerNum <- i
                nodeActorRef <! initmessage
                neighbourList.Add(nodeActorRef) 
        let rand = Random()                    
        let i = rand.Next()%numNodes
        let neighbour = neighbourList.[i]
        let mutable actorMessage = WorkerMessage()
        actorMessage.message <- "You received a Message"  
        actorMessage.algorithm <- "gossip"  
        actorMessage.topology <- topology
        actorMessage.workerNum <-  i
        gotMessage <- gotMessage.Add(i)
        neighbour <! actorMessage


let pushSumTime()    =
        let mutable numNodes = numNodes
        for i =0 to numNodes-1 do
                let mutable neighbourWorkers = new List<int>()
                let actorName = "node"+ string i
                let nodeActorRef =  spawn system actorName pushSumWorker
                if(topology = "full") then           
                    for j = 0 to numNodes-1 do
                        if(j<> i) then
                                neighbourWorkers.Add(j)
                elif(topology = "line") then          
                    if(i <> 0) then           
                        neighbourWorkers.Add(i-1)
                    if(i <> numNodes-1) then    
                        neighbourWorkers.Add(i+1)
                elif(topology = "2D") then             
                        let squareroot = sqrt(float numNodes)
                        if(i % int squareroot <> 0) then
                                neighbourWorkers.Add(i-1)
                        if((i+1) % int squareroot <> 0) then 
                                neighbourWorkers.Add(i+1)
                        if((i- int squareroot )>= 0) then       
                                neighbourWorkers.Add(i-int squareroot)
                        if((i+int squareroot) < numNodes ) then    
                                neighbourWorkers.Add(i+ int squareroot)
                elif(topology = "imp2D") then         
                        let squareroot = sqrt(float numNodes)
                        if(i % int squareroot <> 0) then 
                                neighbourWorkers.Add(i-1)
                        if((i+1) % int squareroot <> 0) then 
                                neighbourWorkers.Add(i+1)
                        if((i- int squareroot )>= 0) then      
                                neighbourWorkers.Add(i-int squareroot)
                        if((i+int squareroot) < numNodes ) then       
                                neighbourWorkers.Add(i+ int squareroot)
                        let rand = Random()
                        let mutable randIndex = neighbourWorkers.[0]
                        while (neighbourWorkers.Contains(randIndex) && randIndex <> i) do 
                                randIndex <- rand.Next()%numNodes
                        neighbourWorkers.Add(randIndex)               
                let mutable initmessage = WorkerMessage()
                initmessage.state <- "init"        
                initmessage.neighbourWorkers<- neighbourWorkers
                initmessage.workerNum <- i
                nodeActorRef <! initmessage
                neighbourList.Add(nodeActorRef) 
        let rand = Random()              
        let i = rand.Next()%numNodes
        let neighbour = neighbourList.[i]
    
        let mutable actorMessage = WorkerMessage()
        actorMessage.message <- "You received a Message"  
        actorMessage.algorithm <- "push-sum"  
        actorMessage.topology <- topology
        actorMessage.workerNum <-  i
        actorMessage.s <- 0.0M
        actorMessage.w <- 0.0M
        gotMessage <- gotMessage.Add(i)
        neighbour <! actorMessage

let totalConvergenceTime(topology,numNodes,algorithm)  = 
        if(algorithm = "gossip") then
            gossipTime()
        elif (algorithm = "push-sum") then
            pushSumTime()    

totalConvergenceTime(topology,numNodes,algorithm) 

Console.ReadLine()

