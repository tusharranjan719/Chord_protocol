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
let rand = System.Random()
let totalHops: int = 0

let chordNode nodeId (mailbox: Actor<_>) = 
    // let mutable id: int = 0
    // let successorNode: IActorRef = null;
    // let successorNodeID: int = 0
    // let mutable fingerTable: IActorRef array = Array.zeroCreate 0

    let mutable successorNode: IActorRef = null;
    let mutable successorNodeID: int = 0
    let mutable predecessorNode: IActorRef = null;
    let mutable predecessorNodeID: int = 0
    let mutable fingerTable = Array.zeroCreate m

    // let findKey =
    //     let stringHash: string = System.Text.Encoding.ASCII.GetBytes $"{nodeId}" |> (new SHA1Managed()).ComputeHash |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x)) |> String.concat System.String.Empty
    //     let asciiList = stringHash |> Seq.map int |> Seq.map(fun(x) -> x * 2)
    //     let sumofList = Seq.sum asciiList
    //     let key = sumofList % m
    //     key

    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
            | MessageType.InitChordMessage msg ->
                let maxNodes = msg.MaxNodes
                let nodesArray = msg.NodesArray
               
                let findSuccessor nodeId =
                    let mutable sid: int = nodeId
                    let mutable successorFound = false
                    let mutable succNodeID = 0
                    while (not (successorFound)) do
                        sid <- sid + 1
                        if sid>=maxNodes then
                            sid <- 0
                        if (not(isNull nodesArray.[sid])) then
                            succNodeID <- sid
                            successorFound <- true
                    done
                    succNodeID

                successorNodeID <- findSuccessor nodeId

                let findPredecesor nodeId =
                    let mutable pid: int = nodeId
                    let mutable predecesorFound = false
                    while (not (predecesorFound)) do
                        pid <- pid - 1
                        if pid<0 then
                            pid <- maxNodes-1
                        if (not(isNull nodesArray.[pid])) then
                            predecessorNodeID <- pid
                            predecesorFound <- true
                    done
                    predecessorNodeID

                for i in fingerTable do
                    let mutable temp = 0
                    temp <- Utils.powOf (2,i) |> int
                    if (not(isNull nodesArray.[nodeId+temp])) then
                        fingerTable.[i] <- nodeId+temp
                    else    
                        fingerTable.[i] <- findSuccessor nodeId+temp

            | MessageType.Ack -> 
                printfn "ll"
            | MessageType.StartStablization ->
                printfn ""
            | _ -> ()
        
        return! loop()
    }
    loop()

let mutable supervisor: IActorRef = null

let Supervisor (mailbox: Actor<_>) = 
    let mutable systemRef = null;
    let mutable totalNumberOfNodes: int = 0
    let mutable numberOfRequests: int = 0
    let mutable maxNodes: int = 0
    let mutable terminateCounter: int = 0
    let mutable nodes = Array.zeroCreate(maxNodes)
    let mutable nodesInChord = Array.zeroCreate(10)
    let createChord totalNumberOfNodes =
        for start in [0 .. totalNumberOfNodes-1] do
            let mutable r = rand.Next(0,maxNodes)
            while(not (isNull nodes.[r])) do
                r <- rand.Next(0,maxNodes)
            nodes.[r]<- chordNode (r)|> spawn system ("Actor"+string(r))
             
    let stabilize node_id =
        printfn "%i" node_id

    let stabilizer =
        let startNodeID = rand.Next(0, nodes.Length-1)
        stabilize startNodeID

    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
            | MessageType.TerminateChordRequest ->
                terminateCounter <- terminateCounter + 1

            | MessageType.InitSupervisor (initMessage) -> 
                printfn "Inside init super"
                systemRef <- mailbox.Sender()

                totalNumberOfNodes <- initMessage.NumberOfNodes
                numberOfRequests <- initMessage.NumberOfRequests

                m <- Utils.log2Ceil (float totalNumberOfNodes)
                maxNodes <- Utils.powOf (2,m) |> int
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
                        //let response = Async.RunSynchronously(nodes.[j] <? MessageType.InitChordInitiation (InitChordMessage))
                        nodes.[j] <! MessageType.InitChordMessage initializeMessage

                stabilizer

            | _ -> ()
        
        return! loop()
    }
    loop()

let main (numberOfNodes, numberOfRequests) = 
    
    if not (isValidInput (numberOfNodes, numberOfRequests)) then
        printfn "Error: Invalid Input"
    else
        supervisor <-spawn system "Supervisor" Supervisor

        let initMessage: MessageType.InitSupervisor = {
            NumberOfNodes = numberOfNodes;
            NumberOfRequests = numberOfRequests;
        }
        printfn "inside main"
        supervisor <! MessageType.InitSupervisor (initMessage)

        System.Console.ReadLine() |> ignore

match fsi.CommandLineArgs with
    | [|_; numberOfNodes; numberOfRequests|] -> 
        main ((Utils.strToInt numberOfNodes), (Utils.strToInt numberOfRequests))
    | _ -> printfn "Error: Invalid Arguments"