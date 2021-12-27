
let rec x n c =
    if c>=n then
        c
     else
        x n (c<<<1)


let p = x 100 1
printfn "%i" p