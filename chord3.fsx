#load @"./MessageType.fsx"
#load @"./Utils.fsx"

#r "nuget: Akka.FSharp"

open Utils
open MessageType
open Akka.FSharp
open Akka.Actor
open System.Security.Cryptography
open System
open System.Collections.Generic

let isValidInput (numberOfNodes, numberOfRequests) =
    (numberOfNodes > 0) && (numberOfRequests > 0)

let system = ActorSystem.Create "System"
let mutable m: int = 0
let mutable maxNodes: int = 0
let rand = System.Random()
let mutable totalHops: int = 0
let mutable nodes = Array.zeroCreate(maxNodes)
let mutable nodesKillProbability: int = -1
//let mutable godStructure = Map.empty
let mutable supervisor: IActorRef = null
let chordNode nodeId (mailbox: Actor<_>) = 
    // let mutable id: int = 0
    // let successorNode: IActorRef = null;
    // let successorNodeID: int = 0
    // let mutable fingerTable: IActorRef array = Array.zeroCreate 0

    let mutable successorNode: IActorRef = null;
    let mutable successorNodeID: int = 0
    let mutable predecessorNode: IActorRef = null;
    let mutable predecessorNodeID: int = 0
    let mutable fingerTable = Array.zeroCreate(m)

    // let findKey =
    //     let stringHash: string = System.Text.Encoding.ASCII.GetBytes $"{nodeId}" |> (new SHA1Managed()).ComputeHash |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x)) |> String.concat System.String.Empty
    //     let asciiList = stringHash |> Seq.map int |> Seq.map(fun(x) -> x * 2)
    //     let sumofList = Seq.sum asciiList
    //     let key = sumofList % m
    //     key
    let closestPrecedingNode (id:int) =
        let mutable closestPreNode = -1
        if (fingerTable.[m-1] <= id && id < nodeId) then
            closestPreNode <- fingerTable.[m-1]
        else
            let mutable predFound = false
            let mutable sortedArray = Array.sort fingerTable
            let mutable loopstart = 1
            while (not (predFound)) do
                if (sortedArray.[loopstart] > id) then
                    closestPreNode <- sortedArray.[loopstart-1]
                    predFound <- true
                    loopstart <- loopstart + 1
                else if (loopstart=sortedArray.Length-1) then
                    closestPreNode <- sortedArray.[m-1]
                    predFound <- true
                    loopstart <- loopstart + 1
                else
                    loopstart <- loopstart + 1
                
        closestPreNode 
                        

        // for i = m-1 downto 0 do
        //     if(fingerTable.[i]>nodeId && fingerTable.[i]<id) then
        //         fingerTable.[i]
        // closestPreNode
    let findSuccessorN nodeId (arr:IActorRef array) =
        let mutable sid: int = nodeId
        let mutable successorFound = false
        let mutable succNodeID = 0
        while (not (successorFound)) do
            sid <- sid + 1
            if sid>=maxNodes then
                sid <- 0
            if (not(isNull arr.[sid])) then
                succNodeID <- sid
                successorFound <- true
        done
        succNodeID
    let findPredecesorN nodeId (arr:IActorRef array)=
        let mutable pid: int = nodeId
        let mutable predecesorFound = false
        let mutable predNodeID = 0
        while (not (predecesorFound)) do
            pid <- pid - 1
            if pid<0 then
                pid <- maxNodes-1
            if (not(isNull arr.[pid])) then
                predNodeID <- pid
                predecesorFound <- true
        done
        predNodeID
    let findFingerTable (arr:IActorRef array)=
         for i in [0..fingerTable.Length-1] do
            let mutable temp = 0
            temp <- Utils.powOf (2,i) |> int
            let mutable pos = (nodeId+temp) % maxNodes
            //printfn “%i” pos
            if (not(isNull arr.[pos])) then
                fingerTable.[i] <- pos
            else
                fingerTable.[i] <- findSuccessorN pos arr


    let findSuccessorID id =
        let mutable succID = -1
        let mutable x = findSuccessorN nodeId nodes
        let mutable y = findSuccessorN id nodes
        if(id <> nodeId) then
            if((id > nodeId && id <= successorNodeID) || x=y) then
               totalHops <- totalHops + 1
               //succID <- successorNodeID
            else
                succID <- closestPrecedingNode id
        succID

    // let nodeJoin =
    //     for i in 0..(nodes.Length-1) do
    //         if(nodes.[i])


    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
            | MessageType.FindKeyMessage msg ->
                totalHops <- totalHops + 1
                let foundID = findSuccessorID msg.RandomKey
                if(foundID <> -1) then
                    msg.NodesArray.[foundID] <! MessageType.FindKeyMessage msg
                else
                    supervisor <! MessageType.TerminateChordRequest

            | MessageType.InitNodeRequests msg ->
                for start in [0..msg.NumberOfRequests-1] do
                    let randomKey = rand.Next(0,maxNodes)
                    let initializeMessage: MessageType.FindKeyMessage = {
                    
                        NodesArray = msg.NodesArray;
                        RandomKey = randomKey;
                    }
                    let foundID: int = findSuccessorID randomKey
                    
                    if(foundID <> -1) then
                        msg.NodesArray.[foundID] <! MessageType.FindKeyMessage initializeMessage
                    else
                        supervisor <! MessageType.TerminateChordRequest

            | MessageType.InitChordMessage msg ->
                let maxNodes = msg.MaxNodes
                let nodesArray = msg.NodesArray
               
                successorNodeID <- findSuccessorN nodeId nodesArray
                predecessorNodeID <- findPredecesorN nodeId nodesArray
                findFingerTable nodesArray
               
            | MessageType.FixFingers msg ->
                let updatedNodesArray = msg.NodesArray
                successorNodeID <- findSuccessorN nodeId updatedNodesArray
                predecessorNodeID <- findPredecesorN nodeId updatedNodesArray
                findFingerTable updatedNodesArray

            | _ -> ()
        
        return! loop()
    }
    loop()



let Supervisor (mailbox: Actor<_>) = 
    let mutable systemRef = null;
    let mutable totalNumberOfNodes: int = 0
    let mutable numberOfRequests: int = 0
    
    let mutable terminateCounter: int = 0
    // let mutable nodes = Array.zeroCreate(maxNodes)
    let mutable nodesInChord = Array.zeroCreate(10)
    let killnodes probability =
        totalNumberOfNodes <- totalNumberOfNodes - int(totalNumberOfNodes * probability)/100

    let createChord totalNumberOfNodes =
        //printfn " max %i" totalNumberOfNodes
        for start in [0 .. totalNumberOfNodes-1] do
            let mutable r = rand.Next(0,maxNodes)
            while(not (isNull nodes.[r])) do
                r <- rand.Next(0,maxNodes)
            nodes.[r]<- chordNode (r)|> spawn system ("Actor"+string(r))

    let stabilizer msg =
        for k in [0..maxNodes-1] do
            if(not(isNull nodes.[k])) then
                nodes.[k] <! MessageType.FixFingers msg

    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
            | MessageType.TerminateChordRequest ->
                terminateCounter <- terminateCounter + 1

                printfn "Total terminateCounter %i" terminateCounter
                if(terminateCounter = (totalNumberOfNodes*numberOfRequests)) then
                    let totalReq = totalNumberOfNodes*numberOfRequests
                    let avgHops = (float totalHops)/(float totalReq)

                    printfn "Average Hops %f" avgHops
                    Environment.Exit 0

            | MessageType.InitSupervisor initMessage -> 
                systemRef <- mailbox.Sender()

                totalNumberOfNodes <- initMessage.NumberOfNodes
                numberOfRequests <- initMessage.NumberOfRequests

                for i in [0..maxNodes-1] do
                    nodes.[i] <- null

                createChord totalNumberOfNodes

                let initializeMessage: MessageType.InitChordMessage = {
                        NumberOfNodes = totalNumberOfNodes;
                        NumberOfRequests = numberOfRequests;
                        NodesArray = nodes;
                        MaxNodes = maxNodes;
                }
                for j in [0..maxNodes-1] do
                    if(not(isNull nodes.[j])) then
                        nodes.[j] <! MessageType.InitChordMessage initializeMessage
                
                if(nodesKillProbability > -1) then
                    if (nodesKillProbability >= 100) then
                        printfn "Average Hops 0"
                        Environment.Exit 0
                    else
                        killnodes nodesKillProbability
                
                stabilizer initializeMessage
                for k in [0..maxNodes-1] do
                    if(not(isNull nodes.[k])) then
                        printfn "%i" k
                        nodes.[k] <! MessageType.InitNodeRequests initializeMessage
                //stabilizer

            | _ -> ()
        
        return! loop()
    }
    loop()

let main (numberOfNodes, numberOfRequests) = 
    
    if not (isValidInput (numberOfNodes, numberOfRequests)) then
        printfn "Error: Invalid Input"
    else
        supervisor <- spawn system "Supervisor" Supervisor

        let initMessage: MessageType.InitSupervisor = {
            NumberOfNodes = numberOfNodes;
            NumberOfRequests = numberOfRequests;
        }

        m <- Utils.log2Ceil (float numberOfNodes)
        maxNodes <- Utils.powOf (2,m) |> int
        nodes <- Array.zeroCreate(maxNodes)
        supervisor <! MessageType.InitSupervisor initMessage

        System.Console.ReadLine() |> ignore

let init =
    // | [|_; numberOfNodes; numberOfRequests; x;|] -> 
        
    //     main ((Utils.strToInt numberOfNodes), (Utils.strToInt numberOfRequests), (Utils.strToInt x))
    //     System.Console.ReadLine() |> ignore
    // | _ -> printfn "Error: Invalid Arguments"
    let args = fsi.CommandLineArgs |> Array.tail
    let mutable numberOfNodes = args.[0] |> int
    let mutable numberOfRequests = args.[1] |> int
    match args.Length with
        | 3 -> 
            nodesKillProbability <- args.[2] |> int
            main (numberOfNodes, numberOfRequests)
        | 2 -> main (20, numberOfRequests)
        | _ -> failwith "You need to pass three parameters!"
    //printfn "%i" nodesKillProbability
init