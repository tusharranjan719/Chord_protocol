#load @"./MessageType.fsx"
#load @"./Utils.fsx"
#r "nuget: Akka.FSharp"

open Utils
open MessageType
open Akka.FSharp
open Akka.Actor
open System.Collections.Generic
open System.Security.Cryptography
open System
open System.Collections.Generic
let system = ActorSystem.Create "System"
let mutable m: int = 0 // power of 2
let mutable maxNodes: int = 0
let rand = System.Random()
let mutable totalHops: int = 0
let mutable nodesKillProbability: int = -1
let mutable supervisor: IActorRef = null

// let mutable testingNodes = Array.zeroCreate(7)
// let dict = new Dictionary<int, Array>()

let isValidInput (numberOfNodes, numberOfRequests) =
    (numberOfNodes > 0) && (numberOfRequests > 0)

let chordNode nodeId (mailbox: Actor<_>) =
    let mutable successorNodeID: int = 0
    let mutable predecessorNodeID: int = 0
    let mutable fingerTable = Array.zeroCreate(m)

    // let findKey =
    //     let stringHash: string = System.Text.Encoding.ASCII.GetBytes $"{nodeId}" |> (new SHA1Managed()).ComputeHash |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x)) |> String.concat System.String.Empty
    //     let asciiList = stringHash |> Seq.map int |> Seq.map(fun(x) -> x * 2)
    //     let sumofList = Seq.sum asciiList
    //     let key = sumofList % m
    //     key

    let closestPrecedingNode (id:int) (arr:IActorRef array)=
        let mutable closestPreNode = -1
        let mutable cid = id
        let mutable closestPredFound = false
        while (not (closestPredFound)) do
            cid <- cid - 1 
            if cid < 0 then
                cid <- maxNodes-1
            if (not(isNull arr.[cid])) then
                for k in fingerTable do
                    if k = cid then
                        closestPreNode <- k
                        closestPredFound <- true
        closestPreNode

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
            //printfn "%i" pos
            if (not(isNull arr.[pos])) then
                fingerTable.[i] <- pos
            else
                fingerTable.[i] <- findSuccessorN pos arr

    let findSuccessorID id (arr:IActorRef array)=
        let mutable succID = -1
        let mutable x = findSuccessorN nodeId arr
        let mutable y = findSuccessorN id arr
        if(id <> nodeId) then

            if((id > nodeId && id <= successorNodeID) || x=y) then
               totalHops <- totalHops + 1
            else
                succID <- closestPrecedingNode id arr
        succID

    // let nodeJoin =
    //     for i in 0..(nodes.Length-1) do
    //         if(nodes.[i])

    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with

            | MessageType.FindKeyMessage msg ->
                totalHops <- totalHops + 1
                let foundID = findSuccessorID msg.RandomKey msg.NodesArray
                if(foundID <> -1) then
                    msg.NodesArray.[foundID] <! MessageType.FindKeyMessage msg
                else
                    supervisor <! MessageType.TerminateChordRequest msg.RandomKey

            | MessageType.InitNodeRequests msg ->
                for start in [0..msg.NumberOfRequests-1] do
                    let randomKey = rand.Next(0,maxNodes)
                    let initializeMessage: MessageType.FindKeyMessage = {
                        NodesArray = msg.NodesArray;
                        RandomKey = randomKey;
                    }
                    // 12 15 13 3 13 9 10 2 1 13 11 15 12 15 14 7 6 15 2 2 
                    // 12 15 13 13 12 3 10 2 13 15 15 14 15 9 6 7 11 2 2
                    let foundID: int = findSuccessorID randomKey msg.NodesArray
                    if(foundID <> -1) then
                        msg.NodesArray.[foundID] <! MessageType.FindKeyMessage initializeMessage
                    else
                        supervisor <! MessageType.TerminateChordRequest randomKey

            | MessageType.InitChordMessage msg ->
                successorNodeID <- findSuccessorN nodeId msg.NodesArray
                predecessorNodeID <- findPredecesorN nodeId msg.NodesArray
                findFingerTable msg.NodesArray

                // testingNodes.[0] <- nodeId
                // testingNodes.[1] <- successorNodeID
                // testingNodes.[2] <- predecessorNodeID
                // testingNodes.[3] <- fingerTable.[0]
                // testingNodes.[4] <- fingerTable.[1]
                // testingNodes.[5] <- fingerTable.[2]
                // testingNodes.[6] <- fingerTable.[3]
                // dict.Add(nodeId, testingNodes)
              

            // | MessageType.FixFingers msg ->
            //     let updatedNodesArray = msg.NodesArray
            //     successorNodeID <- findSuccessorN nodeId updatedNodesArray
            //     predecessorNodeID <- findPredecesorN nodeId updatedNodesArray
            //     findFingerTable updatedNodesArray

            | _ -> ()
        return! loop()
    }
    loop()

let Supervisor (mailbox: Actor<_>) =
    let mutable systemRef = null;
    let mutable totalNumberOfNodes: int = 0
    let mutable numberOfRequests: int = 0
    let mutable terminateCounter: int = 0
    let mutable nodes = Array.zeroCreate(maxNodes)

    let killnodes probability =
        totalNumberOfNodes <- totalNumberOfNodes - int(totalNumberOfNodes * probability)/100
    
    let createChord totalNumberOfNodes =
        for start in [0 .. totalNumberOfNodes-1] do
            let mutable r = rand.Next(0,maxNodes)
            while(not (isNull nodes.[r])) do
                r <- rand.Next(0,maxNodes)
            nodes.[r]<- chordNode (r)|> spawn system ("Actor"+string(r))

    // let stabilizer msg =
    //     for k in [0..maxNodes-1] do
    //         if(not(isNull nodes.[k])) then
    //             nodes.[k] <! MessageType.FixFingers msg

    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
        
            | MessageType.TerminateChordRequest id->
                
                terminateCounter <- terminateCounter + 1
                printfn "Found key %i" id
                //printfn "Total terminateCounter %i" terminateCounter
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
                createChord maxNodes
                let initializeMessage: MessageType.InitChordMessage = {
                        NumberOfNodes = totalNumberOfNodes;
                        NumberOfRequests = numberOfRequests;
                        NodesArray = nodes;
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

                // stabilizer initializeMessage
                // Seq.iter (printfn "nodes,succ,pre,FING = %A") dict.Values
                //Seq.iter (fun arr -> printfn "nodes,succ,pre,FING = %A" arr) dict.Values

                let initializeMessage: MessageType.InitNodeRequests = {
                        NumberOfNodes = totalNumberOfNodes;
                        NumberOfRequests = numberOfRequests;
                        NodesArray = nodes;
                }
                for k in [0..maxNodes-1] do
                    if(not(isNull nodes.[k])) then
                       
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
        supervisor <! MessageType.InitSupervisor initMessage
        System.Console.ReadLine() |> ignore

let init =
    let args = fsi.CommandLineArgs |> Array.tail
    let mutable numberOfNodes = args.[0] |> int
    let mutable numberOfRequests = args.[1] |> int
    main (numberOfNodes, numberOfRequests)
    match args.Length with
        | 3 ->
            nodesKillProbability <- args.[2] |> int
            main (numberOfNodes, numberOfRequests)
        | 2 -> main (numberOfNodes, numberOfRequests)
        | _ -> failwith "Input of form nodes, req, failure accepted"
    printfn "%i" nodesKillProbability
init