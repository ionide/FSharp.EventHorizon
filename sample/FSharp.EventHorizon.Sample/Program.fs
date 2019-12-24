
open System
open EventHorizon.Hole

let mySum (args: int list) = List.sum args

let otherSum = fun (args: int list) -> List.sum args

let inline myMap x = List.filter (fun a -> true) x

let myMap2 = List.map (fun x -> x + 1)

let myMap3 x = List.map (fun x -> 1) x


[<EntryPoint>]
let main argv =
    let result =
        [1 .. 10]
        |> List.map (fun i -> i * i)
        |> hole
        |> List.sum
    printfn "RESULT: %d" result
    0 // return an integer exit code
