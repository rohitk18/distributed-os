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
let randomList = [0..numNodes-1] |> Seq.sortBy(fun x->rand.Next()) |> List

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


// node actor class
let node (nodeId:int)(networkPos:int) (mailbox:Actor<_>) =
// IMP note: the table data contains only nodeids i.e. random ids present in randomList. Its position/index in the randomList is the networkPosition. 
    let mutable state:NodeState = NodeCreated   // state of node based on various message and changes
    let b4Id = ToBase4String(nodeId,digitCount) // base4 version of nodeid
    let smallLeaf:List<int> = new List<int>()   // based on nodeid; max size 4
    let mutable fSmallLeaf:int = -1     // farthest node in small leaf based on nodeid
    let bigLeaf:List<int> = new List<int>()     // based on nodeid; max size 4
    let mutable fBigLeaf:int = numNodes       // farthest node in big leaf based on nodeid
    let routingTable:List<List<int>> = new List<List<int>>()    // based on nodeid; num of rows equal to digit count; number of elements in each row = base = 4
    let neighbourhoodSet:List<int> = new List<int>()    // based on positions; max size 8
    let mutable maxAbsDiff:int = numNodes
    let mutable nodeMaxAbsDiff:int = numNodes
    
    for i = 1 to 4 do
        routingTable.Add(new List<int>([-1;-1;-1;-1]))
    
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
        | StartNetwork ->
            state <- NodeInitialized
        | Route(msg,key) ->
            // key comes as base 4 string
            if key = b4Id then printfn "reached the destination"
            else
                let keyId = FromBase4String key
                match msg with
                | Join ->
                    if state=NodeInitialized then
                    // TODO: Get max and min of lists by writing new helper functions
                        // if key is in vicinity of leaf set of node; updates leaf set
                        if keyId < fBigLeaf && keyId > fSmallLeaf then
                            // no chance of key equality since it is being handled above
                            // keyId less than nodeId
                            if keyId < nodeId then
                                if smallLeaf.Count<4 then 
                                    smallLeaf.Add keyId
                                    fSmallLeaf <- smallLeaf.min
                                else
                                    // if keyId > fSmallLeaf then (* handled above so not needed*)
                                        smallLeaf.Remove fSmallLeaf |> ignore
                                        smallLeaf.Add keyId
                                        fSmallLeaf <- smallLeaf.min
                            // keyId greater than nodeId
                            else
                                if bigLeaf.Count < 4 then
                                    bigLeaf.Add keyId
                                    fBigLeaf <- bigLeaf.max
                                else
                                    // if keyId < fBigLeaf then (* handled above so not needed*)
                                        bigLeaf.Remove fBigLeaf |> ignore
                                        bigLeaf.Add keyId
                                        fBigLeaf <- bigLeaf.max
                        else
                            // updates routing table
                            let mutable b:bool = true
                            let mutable i:int = 0
                            // TODO: routing table update logic
                            // *** //
                            // updates neighbourhood set
                            let keyPos = GetPosition(keyId)
                            if keyPos-networkPos |> Math.Abs < maxAbsDiff
                                neighbourhoodSet.Remove nodeMaxAbsDiff |> ignore
                                neighbourhoodSet.Add keyId
                                // TODO: write functions mentioned below
                                maxAbsDiff <- GetMaxAbsDiff()
                                nodeMaxAbsDiff <- GetNodeMaxAbsDiff()    
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
                let name = "node" + string(randomList.[i])
                spawn system name <| node i randomList.[i])

// printfn "%A" randomList
// printfn "%A" nodes

let startNode = nodes.[0]
startNode <! StartNetwork
startNode <! Route(Join,randomList.[1],1)
    // for i in [1..numNodes-1] do
    //     startNode <! Route(Join,randomList.[i],i)

Console.ReadLine()