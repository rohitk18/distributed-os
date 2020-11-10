#load "Bootstrap.fsx"

open System.Diagnostics
open System
open Akka.FSharp
open Akka.Actor
open System.Linq;
open System.Collections.Generic

let system = ActorSystem.Create("PASTRY")

type PastryMessage = 
    | NodeState of string * string  * bool * int
    | StartJoin 
    | BuildNeighbourhood of List<string> * string
    | BuildRoutingTable of List<List<List<string>>> * string  
    | AddLeafNodes of List<string> * List<string> * string
    | Join of string * int * List<List<List<string>>>
    | RejoinRequest
    | ResetNodeState of string
    | BeginRouting 
    | RouteMessage of string * string * int
    | MsgRoutedToDest of string * int
    | ResetCounters of int * string

let args : string array = fsi.CommandLineArgs |> Array.tail
let numNodes = args.[0] |> int 
let numRequests = args.[1] |> int
let mutable reachedNumRequests = 0
let mutable routingMsgCount = 0 
let timer = Stopwatch()
let random = Random()
let mutable nodeList =  new List<string>()
let mutable nodes =  new Dictionary< string, IActorRef>()
let mutable addedNodeCount = 0
     

let node (inbox: Actor<_>) =
    let mutable nodeValue = ""
    let mutable intnodeValue = 0
    let mutable position =0 
    let mutable neighbourList= new List<string>()
    let mutable adjacentNodeValue = ""
    let mutable smallLeafList =  new List<string>()
    let mutable bigLeafList =  new List<string>()
    let mutable routerTable = new List<List<string>>()
    let mutable isIncluded =  false;
    let mutable reachedMsgCount = 0;
    let mutable deliveredNodeCount =0;
    let mutable allHopsCount =0;
    let mutable allHops = 0;
    let rec loop () = actor {
        let! message = inbox.Receive ()
        match message with
        | NodeState(value,adjacent,nodefailure,pos)  ->            
            nodeValue <- value
            intnodeValue  <- value |> int
            position <- pos
            if(adjacent = "-1") then
                neighbourList <-  new List<string>();
                let mutable i= 0
                while i<=7 do 
                    routerTable.Add(new List<string>())
                    let mutable j=0;
                    while j<=9 do
                        routerTable.ElementAt(i).Add(null)
                        j<-j+1
                    i<-i+1
                smallLeafList <- new List<string>();
                bigLeafList <- new List<string>()
                isIncluded <-true
            else  
                adjacentNodeValue <- adjacent 
                let mutable i= 0
                while i<=7 do 
                    routerTable.Add(new List<string>())
                    let mutable j=0;
                    while j<=9 do
                        routerTable.ElementAt(i).Add(null)
                        j<-j+1
                    i<-i+1

        | StartJoin ->
            if adjacentNodeValue <> "-1" then  
                nodes.[adjacentNodeValue]<! Join(nodeValue, 0, new List<List<List<string>>>())        
        
        | RejoinRequest ->
            nodes.[adjacentNodeValue]<! Join(nodeValue, 0, new List<List<List<string>>>())

        | BuildNeighbourhood(neighbourList, sendernodeValue) ->
            if( neighbourList.Count < 8) then 
                for i in  neighbourList do
                    neighbourList.Add(i)
            else 
                for i in 1..7 do 
                    neighbourList.Add(neighbourList.ElementAt(i))
            neighbourList.Add(sendernodeValue)
        
        | AddLeafNodes(smallerLeafs, greaterLeafs, sendernodeValue) ->
            if (sendernodeValue |> int ) > intnodeValue then
                if(greaterLeafs.Count < 3) then
                    for i in greaterLeafs do 
                        bigLeafList.Add(i)
                else
                    for i in 0..1 do
                        bigLeafList.Add(greaterLeafs.ElementAt(i))                    
                bigLeafList.Add(sendernodeValue)
            else 
                smallLeafList.Add(sendernodeValue)
                if(smallerLeafs.Count < 3) then
                    for i in smallerLeafs do 
                        smallLeafList.Add(i)
                else
                    for i in 1..2 do
                        smallLeafList.Add(smallerLeafs.ElementAt(i))   
            if((sendernodeValue |>int) < intnodeValue) then
                for i in greaterLeafs do 
                    bigLeafList.Add(i)
            else 
                for i in smallerLeafs do 
                    smallLeafList.Add(i)          
            smallLeafList <- smallLeafList.OrderBy(fun n -> n).ToList()                 
            bigLeafList <- bigLeafList.OrderBy(fun n -> n).ToList()                 

        | BuildRoutingTable(routerTableLst,sendernodeValue) ->
            let mutable nodeSet  = new HashSet<string>()
            for routerTables in routerTableLst do
                for r in routerTables do
                    for i in r do  
                        if not (isNull i) then nodeSet.Add(i) |> ignore
            nodeSet.Add(sendernodeValue) |>ignore
            let mutable allNodes = nodeSet.ToList();
            let mutable i =7
            if(position%1000 =0) then
                printf "" 
            while(i>=0) do
                let mutable list = new List<string>(); 
                list <-  allNodes.Where(fun n -> n.StartsWith(nodeValue.Substring(0,i))).ToList();
                allNodes <- allNodes.Where(fun n -> (not (n.StartsWith(nodeValue.Substring(0,i))))).ToList();
                let mutable j=0;
                while j<=9 && list.Count>0 do 
                    let reqNode = list.Where(fun n-> ((string)(n.Chars(i))) = (string)j ).OrderBy(fun n ->  (Math.Abs((n |> int) - (nodeValue |> int ) |> int))).FirstOrDefault(); 
                    routerTable.ElementAt(i).RemoveAt(j)         
                    routerTable.ElementAt(i).Insert(j,reqNode)
                    j<-j+1
                i<-i-1

            let changableNodes = new List<string>();
            for i in 0..(position-1) do
                changableNodes.Add(nodeList.ElementAt(i))
            for i in changableNodes do
                nodes.[i] <! ResetNodeState(nodeValue)
            addedNodeCount <- addedNodeCount+1
            isIncluded <- true
   
        | Join(newnodeValue,pos,hoprouterTableList) ->
            if (not isIncluded && pos=0) then 
                inbox.Sender() <! RejoinRequest 
            else
                let intNewnodeValue  =  newnodeValue |> int
                if pos =0 then 
                   nodes.[newnodeValue] <! BuildNeighbourhood(neighbourList,nodeValue) 
                hoprouterTableList.Add(routerTable)
                if ((intnodeValue>intNewnodeValue && smallLeafList.Count = 0) || (intnodeValue<intNewnodeValue && bigLeafList.Count=0) || intnodeValue= intNewnodeValue) then
                    nodes.[newnodeValue] <! AddLeafNodes(smallLeafList,bigLeafList,nodeValue)
                    nodes.[newnodeValue] <! BuildRoutingTable(hoprouterTableList,nodeValue)
                else
                    let absoluteDifference =  Math.Abs(intnodeValue - intNewnodeValue) 
                    let lowestLeafValue = if (smallLeafList.Count > 0) then   (smallLeafList.ElementAt(0) |>int) else intnodeValue
                    let highestLeafValue = if (bigLeafList.Count > 0) then  (bigLeafList.ElementAt(bigLeafList.Count-1) |>int) else intnodeValue

                    if(lowestLeafValue<intNewnodeValue && highestLeafValue>intNewnodeValue ) then 
                        let mutable minDifference = 0
                        let mutable nextnodeValue =  ""
                        for l in smallLeafList do 
                            let difference  =  Math.Abs((l |> int) - intNewnodeValue)
                            if(difference<minDifference || minDifference = 0) then 
                                minDifference <- difference
                                nextnodeValue <- l
                        for g in bigLeafList do 
                            let difference  =  Math.Abs((g |> int) - intNewnodeValue)
                            if(difference<minDifference || minDifference = 0) then 
                                minDifference <- difference
                                nextnodeValue <- g
                        if (minDifference < absoluteDifference  && minDifference<>0 )then
                            (nodes.[nextnodeValue]) <! Join(newnodeValue,pos+1, hoprouterTableList)
                        else
                            nodes.[newnodeValue] <! AddLeafNodes(smallLeafList,bigLeafList,nodeValue)
                            nodes.[newnodeValue] <! BuildRoutingTable(hoprouterTableList,nodeValue)
                    else
                        let mutable i = 0
                        let mutable dummy = true

                        while i<=7 && dummy do 
                            if(nodeValue.Chars(i)<>newnodeValue.Chars(i)) then 
                                dummy<-false
                            i <- i+1 
                        let nodesList =  routerTable.ElementAt(i-1);
                        let jpos = (newnodeValue.Substring(i-1,1)|>int)
                        let element  =  nodesList.ElementAt(jpos); 
                        if not (isNull element) then 
                            (nodes.[element]) <! Join(newnodeValue,pos+1, hoprouterTableList)
                        else
                            let peerNodes = nodesList.Concat(neighbourList).ToList().Concat(smallLeafList).ToList().Concat(bigLeafList).Where(fun x -> not (isNull x) ).ToList()
                            let mutable minDifference = 0
                            let mutable nextnodeValue =  ""
                            for l in peerNodes do 
                                let difference  =  Math.Abs((l |> int) - intNewnodeValue)
                                if(difference<minDifference || minDifference = 0) then 
                                    minDifference <- difference
                                    nextnodeValue <- l
                            if minDifference < absoluteDifference && minDifference<>0 then
                                (nodes.[nextnodeValue]) <! Join(newnodeValue,pos+1, hoprouterTableList)
                            else
                                nodes.[newnodeValue] <! AddLeafNodes(smallLeafList,bigLeafList,nodeValue)
                                nodes.[newnodeValue] <! BuildRoutingTable(hoprouterTableList,nodeValue)
        | ResetNodeState(newnodeValue) ->  
            let intNewnodeValue = newnodeValue |> int
            let lowestLeafValue = if (smallLeafList.Count > 0) then   (smallLeafList.ElementAt(0) |>int) else intnodeValue
            let highestLeafValue = if (bigLeafList.Count > 0) then  (bigLeafList.ElementAt(bigLeafList.Count-1) |>int) else intnodeValue
            if(smallLeafList.Count<3  && intnodeValue > intNewnodeValue) then
                smallLeafList.Add(newnodeValue)
                smallLeafList <-smallLeafList.OrderBy(fun x -> x).ToList() 
            else if (bigLeafList.Count<3 && intnodeValue <intNewnodeValue) then 
                bigLeafList.Add(newnodeValue)
                bigLeafList <-bigLeafList.OrderBy(fun x -> x).ToList() 

            else if(lowestLeafValue < intNewnodeValue && highestLeafValue> intNewnodeValue) then  
                if(intnodeValue> intNewnodeValue) then 
                    smallLeafList.RemoveAt(0)
                    smallLeafList.Add(newnodeValue)  
                else
                    bigLeafList.RemoveAt(2)
                    bigLeafList.Add(newnodeValue)
                bigLeafList <- bigLeafList.OrderBy(fun x -> x).ToList() 
                smallLeafList <- smallLeafList.OrderBy(fun x -> x).ToList() 

            let mutable i = 0
            let mutable dummy = true
               
            while i<=7 && dummy do 
                if(nodeValue.Chars(i)<>newnodeValue.Chars(i)) then 
                    dummy<-false
                i <- i+1 
            let jpos = (newnodeValue.Substring(i-1,1)|>int)
            let destinationNode  = routerTable.ElementAt(i-1).ElementAt(jpos);
            if (isNull destinationNode) then 
                routerTable.ElementAt(i-1).RemoveAt(jpos);
                routerTable.ElementAt(i-1).Insert(jpos,newnodeValue)
            else if Math.Abs((destinationNode|>int ) - intnodeValue) > Math.Abs(intNewnodeValue - intnodeValue) then
                routerTable.ElementAt(i-1).RemoveAt(jpos);
                routerTable.ElementAt(i-1).Insert(jpos,newnodeValue)

        | BeginRouting ->
            try
                let mutable i = 0
                while(i< numRequests) do
                    let randTarget = random.Next(0, 99999999).ToString("D8")
                    inbox.Self <! RouteMessage(nodeValue,randTarget,0)
                    i<-i+1
            with
                |ex ->
                        raise ex
        | ResetCounters(nodeHopCnt, nodeValue) ->
            allHopsCount <- allHopsCount + nodeHopCnt;
            deliveredNodeCount <- deliveredNodeCount+1
            if(deliveredNodeCount%1000 =0) then
                printf "" 
            if(deliveredNodeCount = numNodes) then
                reachedNumRequests <- deliveredNodeCount
                routingMsgCount <-routingMsgCount+ allHopsCount

        | MsgRoutedToDest(key, numberOfHops) ->
            try
                reachedMsgCount <- reachedMsgCount + 1
                allHops <- allHops + numberOfHops
                if ( reachedMsgCount = numRequests) then   
                    nodes.[nodeList.ElementAt(0)] <! ResetCounters(allHops,nodeValue)

            with
                |ex ->
                        printf ""
                        raise ex
        | RouteMessage(source, destination, latestrouteMsgCount)->

            let intDestination = destination |> int
            if ((intnodeValue>intDestination && smallLeafList.Count = 0) || (intnodeValue<intDestination && bigLeafList.Count=0) || intnodeValue= intDestination) then
                try
                    nodes.[source] <! MsgRoutedToDest(destination,latestrouteMsgCount)
                with 
                    |ex ->
                        printf ""
                        raise ex
            else
                try
                    let absoluteDifference =  Math.Abs(intnodeValue - intDestination) 
                    let lowestLeafValue = if (smallLeafList.Count > 0) then   (smallLeafList.ElementAt(0) |>int) else intnodeValue
                    let highestLeafValue = if (bigLeafList.Count > 0) then  (bigLeafList.ElementAt(bigLeafList.Count-1) |>int) else intnodeValue
                    if(lowestLeafValue<intDestination && highestLeafValue>intDestination ) then 
                        try
                            let mutable minDifference = 0
                            let mutable nextnodeValue =  ""
                            for s in smallLeafList do 
                                let difference  =  Math.Abs((s |> int) - intDestination)
                                if(difference<minDifference || minDifference = 0) then 
                                    minDifference <- difference
                                    nextnodeValue <- s
                            for b in bigLeafList do 
                                let difference  =  Math.Abs((b |> int) - intDestination)
                                if(difference<minDifference || minDifference = 0) then 
                                    minDifference <- difference
                                    nextnodeValue <- b
                            if (minDifference < absoluteDifference  && minDifference<>0 )then
                                (nodes.[nextnodeValue]) <! RouteMessage(source,destination,latestrouteMsgCount+1)
                            else
                                nodes.[source] <! MsgRoutedToDest(destination,latestrouteMsgCount)
                        with
                            |ex ->
                                printf ""
                                raise ex
                    else
                        try
                            let mutable i = 0
                            let mutable dummy = true
                            while i<=7 && dummy do 
                                if(nodeValue.Chars(i)<>destination.Chars(i)) then 
                                    dummy<-false
                                i <- i+1 
                            let nodesList =  routerTable.ElementAt(i-1);
                            let mutable jpos= 0
                            let mutable element =""
                            try
                                jpos <- (destination.Substring(i-1,1)|>int) 
                                element  <-  nodesList.ElementAt(jpos);
                            with
                                | ex ->
                                    printf ""
                                    raise ex
                            if not (isNull element) then      
                                (nodes.[element]) <! RouteMessage(source, destination,latestrouteMsgCount+1)
                            else
                                let peerNodes = nodesList.Concat(neighbourList).ToList().Concat(smallLeafList).ToList().Concat(bigLeafList).Where(fun x ->not (isNull x)).ToList()
                                let mutable minDifference = 0
                                let mutable nextnodeValue =  ""
                                for l in peerNodes do 
                                    let difference  =  Math.Abs((l |> int) - intDestination)
                                    if(difference<minDifference || minDifference = 0) then 
                                        minDifference <- difference
                                        nextnodeValue <- l
                                if minDifference < absoluteDifference && minDifference<>0 then
                                    (nodes.[nextnodeValue]) <! RouteMessage(source, destination,latestrouteMsgCount+1)
                                else
                                    nodes.[source] <! MsgRoutedToDest(destination,latestrouteMsgCount)
                        with 
                            |ex ->
                                printf ""
                                raise ex
                with
                    |ex ->
                        printf ""
                        raise ex
        return! loop ()
    }
    loop ()

let mutable nodeId = ""
for i in 1 .. numNodes do 
    nodeId <- random.Next(0, 99999999).ToString("D8")
    while(nodeId.Length <> 8 || nodeList.Contains(nodeId)) do 
        nodeId <- random.Next(0, 99999999).ToString("D8")
    nodeList.Add(nodeId)

for i in nodeList.OrderBy(fun x -> x) do 
    printf ""

for i in 1 .. numNodes do 
    let nodeString = "worker"+ sprintf "%i" i
    let nodeRef = spawn system nodeString node
    let nodeValueNum =  nodeList.ElementAt(i-1)
    nodes.Add(nodeValueNum,nodeRef)
    let mutable adjacentNode = ""
    if(i=1) then
        adjacentNode <- "-1"
    else 
        adjacentNode <- nodeList.ElementAt(i-2)
    nodeRef <! NodeState (nodeValueNum,adjacentNode,false,i-1)
timer.Start()
for i in 1..(numNodes-1) do 
    nodes.[nodeList.ElementAt(i)] <! StartJoin

while addedNodeCount <> numNodes-1 do ()
printf "" 

printf ""
for i in 1..(numNodes) do 
    nodes.[nodeList.ElementAt(i-1)] <! BeginRouting
while reachedNumRequests <> numNodes  do ()
printfn "Average Number of Hops %A" ((routingMsgCount|> float)/((numNodes|>float)*(numRequests |> float)))
system.Terminate ()

   
    

