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
    | Updated

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
    let n = randomList.BinarySearch num
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
    let neighbourhoodSet:List<string> = new List<string>()    // based on positions; max size 8
    let mutable maxAbsDiff:int = numNodes
    let mutable nodeMaxAbsDiff:string = null
    
    for i = 1 to 4 do
        routingTable.Add(new List<string>(["-1";"-1";"-1";"-1"]))
    
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
        | StartNetwork ->
            state <- NodeInitialized
            // let selectedNode = mailbox.Context.System.ActorSelection("user/"+ToBase4String(randomList.[1],digitCount))
            // printfn "selected node - %A" selectedNode
            // if nodeId <> randomList.[1] then selectedNode <! StartNetwork
            // else printfn "actor selection test %A" b4Id
        | Route(msg,key) ->
            // key valid or invalid
            // ***
            // key comes as base 4 string
            if key = b4Id then printfn "reached the destination"
            else
                printfn "%A" key
                let keyNum = FromBase4String key
                match msg with
                | Join ->
                    if state=NodeInitialized then
                        printfn "going into initialized"
                        // if key is in vicinity of leaf set of node; updates leaf set
                        // *** //
                        if key.CompareTo(b4Id) = -1 then
                            printfn "smallleaf"
                            if isNull fSmallLeaf then
                                printfn "null case"
                                smallLeaf.Add key
                                printfn "working add %A" smallLeaf
                                fSmallLeaf <- key
                                printfn "fSmallleaf %A" fSmallLeaf
                            else 
                                if smallLeaf.Count >= 4 then smallLeaf.Remove fSmallLeaf |> ignore
                                smallLeaf.Add key
                                smallLeaf.Sort()
                                fSmallLeaf <- smallLeaf.[0]
                        else
                            printfn "bigleaf"
                            if isNull fBigLeaf then
                                printfn "null case"
                                bigLeaf.Add key
                                printfn "working add %A" bigLeaf
                                fBigLeaf <- key
                                printfn "fBigleaf %A" fBigLeaf
                            else
                                if bigLeaf.Count >= 4 then bigLeaf.Remove fBigLeaf |> ignore
                                bigLeaf.Add key
                                bigLeaf.Sort()
                                fBigLeaf <- bigLeaf.[bigLeaf.Count-1]
                        // *** //

                        // updates routing table
                        // *** //
                        let mutable b:bool = false
                        let mutable i:int = 0
                        while not b do
                            // what if i gets to end? only possible when key is equal to node's base 4 id which is handled above
                            printfn "in routing table row %A" i
                            if key.[i]<>b4Id.[i] then
                                printfn "not matched"
                                let num = int(string(key.[i]))   // gets the digit
                                if routingTable.[i].[num] = "-1" then 
                                    routingTable.[i].[num] <- key
                                else
                                    let diffk = Math.Abs(keyNum-nodeId)
                                    let diffr = Math.Abs(FromBase4String(routingTable.[i].[num])-nodeId)
                                    // TODO: send join message to update node join and hopping calculation
                                    mailbox.Context.System.ActorSelection("user/"+ routingTable.[i].[num]) <! Route(Join,key)
                                    // printfn "selected node - %A" selectedNode
                                    if diffk < diffr then routingTable.[i].[num] <- key // updates the node reference
                                b <-true    // stops loop
                                printfn "not matched done"
                            else
                                printfn "matched"
                                i <- i+1    // next digit and also next row
                                printfn "matched done"
                        // *** //

                        // updates neighbourhood set
                        // *** //
                        let keyPos = GetPosition(keyNum)
                        if Math.Abs(keyPos-networkPos) < maxAbsDiff then
                            if neighbourhoodSet.Count >= 8 then neighbourhoodSet.Remove nodeMaxAbsDiff |> ignore // will not find for first time since it doesn't exist but will find for subsequent routines since something will exist
                            neighbourhoodSet.Add key
                            neighbourhoodSet.Sort()
                            let diffMin = nodeId - FromBase4String(neighbourhoodSet.[0])
                            let diffMax = FromBase4String(neighbourhoodSet.[neighbourhoodSet.Count-1]) - nodeId
                            if diffMin > diffMax then 
                                maxAbsDiff <- diffMin
                                nodeMaxAbsDiff <- neighbourhoodSet.[0]
                            else
                                maxAbsDiff <- diffMax
                                nodeMaxAbsDiff <- neighbourhoodSet.[neighbourhoodSet.Count-1]
                        // *** //
                        printfn "node of key %A is joined" key
                    else printfn "please wait for node to join network"
                | _ -> printfn "Received Unknown Request Message"
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
startNode <! StartNetwork
startNode <! Route(Join,ToBase4String(randomList.[1],digitCount))
    // for i in [1..numNodes-1] do
    //     startNode <! Route(Join,randomList.[i],i)

// network manager actor
let networkManager (mailbox:Actor<_>) = 
    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
        | _ -> printfn "%A" message
        return! loop()
    }
    loop()
let nmActor = spawn system "NetworkManager" networkManager

Console.ReadLine()