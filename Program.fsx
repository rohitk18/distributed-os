#time "on"
#load "Bootstrap.fsx"

open System


let sumOfSquares (n:uint):uint =
    (* Sum of squares of positive integers from 1 to n (including) *)
    match n with
    | 0u -> 0u
    | _ -> n * (n+1u) * (2u*n + 1u) / 6u

let sumOfSquaresRange (n:uint,k:uint):uint = 
    (* Sum of squares of integers from n to n+k-1 *)
    match k with
    | 0u -> 0u
    | 1u -> sumOfSquares n
    | _ -> sumOfSquares(n+k-1u) - sumOfSquares(n-1u)


[<EntryPoint>]
let main argv =

    // defined unsigned int to input numbers since only positive integers are being involved
    let n:uint = uint argv.[0]
    let k:uint = uint argv.[1]
    Console.WriteLine("{0}",sumOfSquaresRange(n,k))

    0 // return an integer exit code
