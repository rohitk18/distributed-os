Project Members:

Rohit Karumuri(UFID:9097-1158)
Spandana Edupuganti(UFID:1733-6715)

Design of Program:

For file proj1.fsx
• It involves "calculator" i.e. the worker actor model.
• The program divides the calculation to 8 actors.
• The boss actor requests each worker with the parameters: start number and length of numbers to work on.
• Each request response in recorded and output is printed on console.

For file remoteproj1.fsx
• This executes actors in the remote system.
• Configuration is defined and worker actors are created by boss in remote system.

Results:
1.
No. of workers = maximum of 8
Size of the work Unit = given input/no.of workers
                     =N/8
Each worker is given N/8 subproblems to solve. Boss creates 8 workers and assigns (N/8) subproblems to each one of them.
For N = 1000000 and k=4, work unit size is 125000

2.
The result of running the program for
dotnet fsi proj1.fsx 1000000 4 

There is no output for this particular N and K as there is no sequence of consecutive numbers whose sum of squares is a perfect square.

3.
For proj1.fsx (local)
The running time for N=1000000 and k=4 is
CPU Time = 6265 ms
Real Time = 2004 ms
No.of Cores Used=CPU Time/Real Time 
                = 3.12

For remoteproj1.fsx (remote) 
The running time for N=1000000 and k=4 is
CPU Time = 5359 ms
Real Time =1523 ms
No.of Cores Used=CPU Time/Real Time 
                = 3.159

4.
The largest problem we managed to solve is for
N=10000000 K=24
CPU Time = 62750
Real Time = 14401
No of Cores used = CPU Time/ Real Time
		= 4.357




 
