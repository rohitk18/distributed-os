# Distributed Operating System Principles Project 3: Pastry

## Group members :
- 1.Rohit Karumuri - 9097-1158
- 2.Spandana Edupuganti - 1733-6715

## Instructions to run code :
    1)Open command Window and move to the directory of the project
    2)Run the following command 
        dotnet fsi proj3.fsx numNodes numRequests    
    
## What is working?
    1) Pastry API for join and routing in the network are working as described in the given pastry paper.
    2) All the nodes provided as input(numNodes) join the network and each request is routed to the node that have nearest value to the given key with the help of the routing table of the respective node.
    4) The Average number of hops that have to be traversed to deliver a message is calculated and printed to console.

## The largest problem we managed to deal with
   Largest problem we were able deal with is for 10000 nodes and 10 requests
   Average Number of Hops for 10000 nodes is 3.866