#load "Bootstrap.fsx"

open System
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp

type NodeState = 
    | NodeCreated

type NodeMessage =
    | FrontNode of node : IActorRef
    | BackNode of node : IActorRef
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

let node (nodeId:int)(digitCount:int) (mailbox:Actor<_>) = 
    let smallLeaf:List<int> = new List<int>()
    let bigLeaf:List<int> = new List<int>()
    let routingTable:List<List<int>> = new List<List<int>>()
    let mutable state:NodeState = NodeCreated
    let mutable nodeFront:IActorRef = null
    let mutable nodeBack:IActorRef = null
    let b4Id = ToBase4String(nodeId,digitCount)
    for i = 1 to 4 do
        routingTable.Add(new List<int>([-1;-1;-1;-1]))
    
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
        | FrontNode(node) ->
            nodeFront <- node
            printfn "%A" nodeFront            
        | BackNode(node) ->
            nodeBack <- node
            printfn "%A" nodeBack
        | _ -> printfn "Received Unknown Message"
        return! loop()
    }
    loop()

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

    // let groupOne = new List<int>()
    // let groupOneSize = if numNodes <= 1024 then numNodes else 1024

    // for i in 0..groupOneSize-1 do
    //     groupOne.Add(randomList.[i])
    
// list of references of actor nodes
let nodes = [0..numNodes-1]
            |> List.map (fun i ->
                let name = "node" + string(randomList.[i])
                spawn system name <| node i digitCount)

// building network by adding routes in each node step by step
let network = List<IActorRef>()
    
// printfn "%A" randomList
// printfn "%A" nodes

Console.ReadLine()