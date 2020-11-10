#load "Bootstrap.fsx"

open System
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp

type NodeState = 
    | NodeCreated
    | NodeInitialized

type NodeMessage =
    | StartNetwork
    | Route of message:NodeMessage * key:string
    | Join
    | SendState of receivernid:string * smallLeaf:List<string> * bigLeaf:List<string> * routingTable:List<List<string>> * neighbourhoodSet:List<string> * toNew:bool
    | Updated
    | Print

// global variables
let numNodes:int = int(fsi.CommandLineArgs.[1])
// let numRequests:int = int(fsi.CommandLineArgs.[2])

// network initialization
let system:ActorSystem = ActorSystem.Create("pastry")
// number of digits in the node id
let digitCount:int = int(Math.Ceiling(Math.Log(float(numNodes),4.0)))
// max number of nodes that can be created with the available digit count
let nodeSpace:int = Math.Pow(4.0,float(digitCount)) |> int
    
let rand = Random();
// stores list of random numbers
let randomList = [0..nodeSpace-1] |> Seq.sortBy(fun x->rand.Next()) |> List
randomList = randomList.GetRange(0,numNodes)

// building network by adding routes in each node step by step
let network = List<IActorRef>()

// network manager actor
let networkManager (mailbox:Actor<_>) = 
    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
        // | AddToNetwork(key) ->
        //     network.
        | _ -> printfn "%A" message
        return! loop()
    }
    loop()
let nmActor = spawn system "NetworkManager" networkManager

// helper functions
// to base 4 string
let ToBase4String (num:int,length:int) :string =
    let digits = ["0";"1";"2";"3"]
    let mutable n:int = num
    let mutable s:string = String.Empty
    while (n>0) do
        s <- digits.[n%digits.Length] + s
        n <- n/digits.Length
    s <- String('0',length-s.Length) + s
    s

// from base 4 string
let FromBase4String (s:string):int = 
    let mutable n = 0
    for i in [s.Length..0] do
        n<- int(s.[i]) + 4*n
    n

// get position of key by getting index in randomList
let GetPosition(num:int):int = 
    let n = randomList.IndexOf num
    n

// // max of list
// let GetMax(l:int list):int =
//     let mutable m = l.[0]
//     for i in l do
//         if m < i then m <- i
//     m

// // min of list
// let GetMin(l:int list):int =
//     let mutable m = l.[0]
//     for i in l do
//         if m > i then m <- i
//     m

// find max absolute difference and the corresponding node
(*let GetMaxAbsDiff(neighbourhoodSet:int list, np:int) =
    let mutable min = numNodes
    let mutable n
    for num in neighbourhoodSet do
        let absDiff = GetPosition(num) - np |> Math.abs
        if absDiff < min then
            min <- absDiff
            n <- num
    (n,min) *)

// node actor class
let node (nodeId:int)(networkPos:int) (mailbox:Actor<_>) =
// IMP note: the table data contains only nodeids/keys i.e. base4 strings of random ints present in randomList. Its position/index in the randomList is the networkPosition. 
    let mutable state:NodeState = NodeCreated   // state of node based on various message and changes
    let b4Id = ToBase4String(nodeId,digitCount) // base4 version of nodeid
    let smallLeaf:List<string> = new List<string>()   // based on nodeid; max size 4; keeping list sorted
    let mutable fSmallLeaf:string = null     // farthest node in small leaf based on nodeid
    let bigLeaf:List<string> = new List<string>()     // based on nodeid; max size 4; keeping list sorted
    let mutable fBigLeaf:string = null       // farthest node in big leaf based on nodeid
    let routingTable:List<List<string>> = new List<List<string>>()    // based on nodeid; num of rows equal to digit count; number of elements in each row = base = 4
    let mutable neighbourhoodSet:List<string> = new List<string>()    // based on positions; max size 8
    let mutable maxAbsDiff:int = nodeSpace+2
    let mutable nodeMaxAbsDiff:string = null
    
    for i = 1 to digitCount do
        routingTable.Add(new List<string>(["-1";"-1";"-1";"-1"]))
    
    // add keys to each list
    let addKeyToLeaf(key:string) =
        if key.CompareTo(b4Id) = -1 && not(smallLeaf.Contains(key)) then
            if isNull fSmallLeaf then
                smallLeaf.Add key
                fSmallLeaf <- key
            else 
                if smallLeaf.Count >= 4 then smallLeaf.Remove fSmallLeaf |> ignore
                smallLeaf.Add key
                smallLeaf.Sort()
                fSmallLeaf <- smallLeaf.[0]
        elif key.CompareTo(b4Id) = 1 && not(bigLeaf.Contains(key)) then
            if isNull fBigLeaf then
                bigLeaf.Add key
                fBigLeaf <- key
            else
                if bigLeaf.Count >= 4 then bigLeaf.Remove fBigLeaf |> ignore
                bigLeaf.Add key
                bigLeaf.Sort()
                fBigLeaf <- bigLeaf.[bigLeaf.Count-1]

    let addKeyToRoutingTable(key:string,keyNum:int,toSend:bool) =
        let mutable b:bool = false
        let mutable i:int = 0
        while not(b) do
            // what if i gets to end? only possible when key is equal to node's base 4 id which is handled above
            if key.[i]<>b4Id.[i] then
                let num = int(string(key.[i]))   // gets the digit
                if routingTable.[i].[num] = "-1" then 
                    routingTable.[i].[num] <- key
                else
                    let diffk = Math.Abs(keyNum-nodeId)
                    let diffr = Math.Abs(FromBase4String(routingTable.[i].[num])-nodeId)
                    if diffk < diffr then routingTable.[i].[num] <- key // updates the node reference
                    else if toSend then mailbox.Context.System.ActorSelection("user/"+ routingTable.[i].[num]) <! Route(Join,key) 
                b <-true    // stops loop
            else
                i <- i+1    // next digit and also next row
    
    let addKeyToNS(key:string, keyNum:int) = 
        let keyPos = GetPosition(keyNum)       
        if not(neighbourhoodSet.Contains(key)) then
            let ns:List<string> = new List<string>(neighbourhoodSet)
            if ns.Count >= 8 && Math.Abs(keyPos-networkPos) <= maxAbsDiff then ns.Remove nodeMaxAbsDiff |> ignore // will not find for first time since it doesn't exist but will find for subsequent routines since something will exist
            ns.Add key
            maxAbsDiff <- 0
            for k in neighbourhoodSet do
                let diff = Math.Abs(GetPosition(FromBase4String(k))-keyPos)
                if diff > maxAbsDiff then
                    maxAbsDiff <- diff
                    nodeMaxAbsDiff <- k
            neighbourhoodSet <- ns

    // according to assumed network    
    if networkPos>0 && state=NodeCreated then
        let pnodeNum = randomList.[networkPos-1]
        let pnode = ToBase4String(pnodeNum,digitCount)
        addKeyToLeaf(pnode)
        addKeyToRoutingTable(pnode,pnodeNum,false)
        addKeyToNS(pnode,pnodeNum)

    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
        | StartNetwork ->
            state <- NodeInitialized
            // let selectedNode = mailbox.Context.System.ActorSelection("user/"+ToBase4String(randomList.[1],digitCount))
            // printfn "selected node - %A" selectedNode
            // if nodeId <> randomList.[1] then selectedNode <! StartNetwork
            // else printfn "actor selection test %A" b4Id
        | SendState(rnid,sl,bl,rt,ns,toNew)->
            // two types of nodes get received, newly joined nodes and already existing nodes
            // printfn "\nstate operations for %A" b4Id
            // printfn "sl %A" sl
            // printfn "bl %A" bl
            // printfn "rt %A" rt
            // printfn "ns %A" ns
            let nodeList:List<string> = new List<string>()
            nodeList.Add(rnid)
            for node in sl do nodeList.Add(node)
            for node in bl do if not(nodeList.Contains(node)) then nodeList.Add(node)
            for row in rt do for node in row do if not(nodeList.Contains(node)) && node<>"-1" then nodeList.Add(node)
            for node in ns do if not(nodeList.Contains(node)) then nodeList.Add(node)
            for node in nodeList do
                if node <> b4Id then
                    let nodeNum = FromBase4String(node)
                    addKeyToLeaf(node)
                    addKeyToRoutingTable(node,nodeNum,false)
                    addKeyToNS(node,nodeNum)
            // change state of node from created to initialized for new ones also check using node state to ignore toNew flag   
            // reason: since multiple nodes send state to newly joined node, keep the toNew flag
            if toNew then
                state <- NodeInitialized
                printfn "sending to sender"
                mailbox.Sender() <! SendState(b4Id,smallLeaf,bigLeaf,routingTable,neighbourhoodSet,false)
            else 
                printfn "updated and stopped here"

        | Route(msg,key) ->
            // key valid or invalid
            // ***
            // key comes as base 4 string
            if key = b4Id then printfn "routed to new joining node. Check for conflicts"
            else
                printfn "%A" key
                let keyNum = FromBase4String key
                match msg with
                | Join ->
                    if state=NodeInitialized then
                        // if key is in vicinity of leaf set of node; updates leaf set
                        // *** //
                        addKeyToLeaf(key)        
                        // *** //

                        // updates routing table
                        // *** //
                        addKeyToRoutingTable(key,keyNum,true)
                        // *** //

                        // updates neighbourhood set
                        // *** //
                        addKeyToNS(key,keyNum)
                        // *** //
                        // send state tables to the key node
                        mailbox.Context.System.ActorSelection("user/"+key) <! SendState(b4Id,smallLeaf,bigLeaf,routingTable,neighbourhoodSet,true)
                        printfn "node of key %A is joined" key
                    else 
                        printfn "please wait for node to join network"
                        while state = NodeCreated do
                            // printf ""
                            Threading.Thread.Sleep 1000
                        mailbox.Self <! Route(Join,key)
                | _ -> printfn "Received Unknown Request Message"
        // for debugging
        | Print ->
            printfn "\nNode %A %A %A" b4Id nodeId networkPos
            printfn "SmallLeaf %A" smallLeaf
            printfn "BigLeaf %A" bigLeaf
            printfn "Routing Table %A" routingTable
            printfn "NeighbourhoodSet %A" neighbourhoodSet           
            printfn "maxdiff %A %A" maxAbsDiff nodeMaxAbsDiff
            // printfn "NeighbourhoodSetpos %A" <|  neighbourhoodSet 
        | _ -> printfn "Received Unknown Message"
        return! loop()
    }
    loop()
    
// list of references of actor nodes
let nodes = [0..numNodes-1]
            |> List.map (fun i ->
                // let name = "node" + string(randomList.[i])
                let name = ToBase4String(randomList.[i], digitCount)
                spawn system name <| node randomList.[i] i)

// printfn "%A" randomList
printfn "%A" nodes

let startNode = nodes.[0]
network.Add(startNode)
startNode <! StartNetwork
// Console.ReadLine()
// startNode <! Route(Join,ToBase4String(randomList.[1],digitCount))
// Console.ReadLine()
// nodes.[1] <! Route(Join,ToBase4String(randomList.[2],digitCount))
for i in [1..numNodes-1] do
    Threading.Thread.Sleep(i*100)
    nodes.[i-1] <! Route(Join,ToBase4String(randomList.[i],digitCount))
    network.Add(nodes.[i])
    if i%15=0 then Threading.Thread.Sleep 2000
    if i%50=0 then Threading.Thread.Sleep 5000

Console.ReadLine()

printfn "*************************************************************"
for i in [0..numNodes-1] do
    Threading.Thread.Sleep 10
    nodes.[i] <! Print
Console.ReadLine()