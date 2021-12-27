open System

module Utils =
    let rec getPower2Round n c =
        if(c>=n)
        then c
        else
            getPower2Round n (c <<< 1)
    
    let rec calculateM value i =
        if (1 <<< i) >= value 
        then i
        else
            calculateM value (i + 1)

    let log2Ceil v =
        Math.Log(v, 2.) |> Math.Ceiling |> Convert.ToInt32

module Main = 
    let fun1 = 
        let x = Utils.getPower2Round 50 1
        let y = Utils.log2Ceil 50.0
        printfn "%i" y

Main.fun1


