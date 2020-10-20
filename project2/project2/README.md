# Distributed Operating System Principles Project 2: Gossip Simulator

## Group members :
- 1.Rohit Karumuri - 9097-1158
- 2.Spandana Edupuganti - 1733-6715

## Instructions to run code :
    1)Open command Window and move to the directory of the project
    2)Run the following command 
        dotnet fsi proj2.fsx numNodes topology algorithm    
    
## What is working?
    Gossip and Push-sum algorithms with 4 topologies Full network, 2D Grid, Line and Imperfect 2D Grid are working.

## The largest problem we managed to solve is for the following number of input nodes
   For Gossip Algorithm:
        1. Full: 10000
        2. Line: 20000
        3. 2D : 20000
        4. Imperfect 2D: 15000
    For Push-sum Algorithm:
        1. Full : 5000 
        2. 2D : 500 
        3. Line: 120 
        4. Imperfect 2D : 2000 