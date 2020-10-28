// Learn more about F# at http://fsharp.org

open System
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp

type NodeState = 
    | NodeCreated
    | NodeInitialized

type NodeMessage =
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

type Printer =
    inherit Actor
    override x.OnReceive message =
        match message with
        | :? string as msg -> printfn "%A" msg
        | _ -> printfn "Unknown message"

let node (nodeId:int)(networkPos:int) (mailbox:Actor<_>) = 
    let smallLeaf:List<int> = new List<int>()
    let bigLeaf:List<int> = new List<int>()
    let routingTable:List<List<int>> = new List<List<int>>()
    let mutable state:NodeState = NodeCreated
    let b4Id = ToBase4String(nodeId,8)
    for i = 1 to 4 do
        routingTable.Add(new List<int>([-1;-1;-1;-1]))
    
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
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

    // building network by adding routes in each node step by step
    let network = List<IActorRef>()
    
    // printfn "%A" randomList
    // printfn "%A" nodes
    
    

    Console.ReadKey(false) |> ignore

    0 // return an integer exit code
