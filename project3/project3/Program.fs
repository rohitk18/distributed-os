// Learn more about F# at http://fsharp.org

open System
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp

type NodeState = 
    | NodeCreated
    | NodeInitialized

type NodeMessage =
    | StartNetwork
    | Route of message:NodeMessage * key:int * pos:int
    | Join
    | Updated

let ToBase4String (num:int,length:int) :string =
    let digits = ["0";"1";"2";"3"]
    let mutable n:int = num
    let mutable s:string = String.Empty
    while (n>0) do
        s <- digits.[n%digits.Length] + s
        n <- n/digits.Length
    s <- String('0',length-s.Length) + s
    s

// let checkPosition (num:int):int = 


// building network by adding routes in each node step by step
let network = List<IActorRef>()

let node (nodeId:int)(networkPos:int) (mailbox:Actor<_>) = 
    // IMP note: the table data contains only nodeids i.e. random ids present in randomList. Its position/index in the randomList is the networkPosition.
    let smallLeaf:List<int> = new List<int>()
    let mutable farthestSmallLeaf:int = -1
    let bigLeaf:List<int> = new List<int>()
    let routingTable:List<List<int>> = new List<List<int>>()
    let neighbourhoodSet:List<int> = new List<int>()
    let mutable state:NodeState = NodeCreated
    let b4Id = ToBase4String(nodeId,8)
    for i = 1 to 4 do
        routingTable.Add(new List<int>([-1;-1;-1;-1]))
    
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
        | StartNetwork ->
            state <- NodeInitialized
        | Route(msg,key,pos) ->
            match msg with
            | Join ->
                if state=NodeInitialized then
                    // if key
                    printfn "node of key %A is joined" key
                else printfn "please wait for node to join network"
            | _ -> printfn "Received Unknown Request Message"
        | _ -> printfn "Received Unknown Message"
        return! loop()
    }
    loop()


[<EntryPoint>]
let main argv =

    let numNodes:int = int(argv.[0])
    // let numRequests:int = int(argv.[1])

    // network initialization
    let system:ActorSystem = ActorSystem.Create("pastry")
    // number of digits in the node id
    let digitCount:int = int(Math.Ceiling(Math.Log(float(numNodes),4.0)))
    // max number of nodes that can be created with the available digit count
    let nodeSpace:int = Math.Pow(4.0,float(digitCount)) |> int
    
    let rand = Random();
    // stores list of random numbers
    let randomList = [0..numNodes-1] |> Seq.sortBy(fun x->rand.Next()) |> List
    
    // list of references of actor nodes
    let nodes = [0..numNodes-1]
                |> List.map (fun i ->
                    let name = "node" + string(randomList.[i])
                    spawn system name <| node i randomList.[i])

    let startNode = nodes.[0]
    startNode <! StartNetwork
    startNode <! Route(Join,randomList.[1],1)
    // for i in [1..numNodes-1] do
    //     startNode <! Route(Join,randomList.[i],i)

    let s = "String"
    printfn "%A" s.[2]

    
    // printfn "%A" randomList
    // printfn "%A" nodes
    
    

    Console.ReadKey(false) |> ignore

    0 // return an integer exit code
