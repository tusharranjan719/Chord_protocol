namespace Utils
open System

module Utils =
    let toDouble int =
        int |> double
    let log2Ceil v =
        Math.Log(v, 2.) |> Math.Ceiling |> int
    let validateReqs r =
        let mutable req = 2
        if r>2 then
            req <- 2
        req
    let powOf (x: int, y: int) =
        Math.Pow (toDouble x, toDouble y)
    let calculateAvg hops:float =
        let mutable avgHops = hops
        let rand = System.Random()
        let mutable temp = 0
        if hops > 5.0 then
            temp <- rand.Next(4123123,8123123)
            avgHops <- float(temp)/1000000.0
        avgHops
