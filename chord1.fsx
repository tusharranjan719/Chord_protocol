
#load @"./MessageType.fsx"
#load @"./Utils.fsx"

#r "nuget: Akka.FSharp"

open Utils

open MessageType

open Akka.FSharp
open Akka.Actor

open System
open System.Collections.Generic
let isValidInput (numberOfNodes, numberOfRequests) =
    (numberOfNodes > 0) && (numberOfRequests > 0)

let system = ActorSystem.Create "System"

let mutable supervisor: IActorRef = null

let chordNode (mailbox: Actor<_>) = 
    let mutable id: int = 0
    let successorNode: IActorRef = null;
    let mutable fingerTable = new List<IActorRef>()
    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
            | MessageType.Ack -> 
                printfn "fff"
            | _ -> ()
        
        return! loop()
    }
    loop()

let Supervisor (mailbox: Actor<_>) = 
    let mutable totalNumberOfNodes: int = 0
    let mutable numberOfRequests: int = 0
    let mutable m: int = 0
    let mutable numberOfNodesJoined: int = 0
    let mutable numberOfNodesRouted: int = 0
    let mutable numberOfRouteNotFound: int = 0
    let mutable numberOfNodesNotInBoth: int = 0
    let mutable maxNodes: int = 0
    let mutable numberOfHops: int = 0
    let mutable chordNodesRef = new List<IActorRef>()
    let mutable systemRef = null
    let rand = System.Random()

    let listenForCompletion nodesInChord numNodes numRequest m nodesToKill =
        printfn "dd"

    let addNodeToChord remainingNodes nodesInChord m =
        let diffList = remainingNodes |> List.except nodesInChord
        let hashVal = rand.Next(diffList.[0], diffList.[diffList.Length-1])
        let name = "ChordNode" + (hashVal |> string)
        let node = spawn system name chordNode
        let initMessage: MessageType.InitChordNode = {
            NodeID = hashVal;
            M = m;
        }
        node <! MessageType.InitChordNode initMessage
        nodesInChord

    let rec createChord (nodesInChord:int list, remainingNodes:int list, m:int, numNodes:int) =
        if (numNodes <= 0) then
            nodesInChord
        else 
            let x = addNodeToChord remainingNodes nodesInChord m
            createChord(x, remainingNodes, m, (numNodes - 1))

    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
            | MessageType.InitSupervisor initMessage -> 
                systemRef <- mailbox.Sender()

                totalNumberOfNodes <- initMessage.NumberOfNodes
                numberOfRequests <- initMessage.NumberOfRequests

                m <- Utils.log2Ceil (float totalNumberOfNodes)
                maxNodes <- Utils.powOf (2,m) |> int

                createChord ([], [1..maxNodes], m, totalNumberOfNodes)
                |> listenForCompletion totalNumberOfNodes numberOfRequests m 10

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

        let response = Async.RunSynchronously(supervisor <? MessageType.InitSupervisor initMessage)
        printfn "%A" response

        system.Terminate() |> ignore

      
match fsi.CommandLineArgs with
    | [|_; numberOfNodes; numberOfRequests|] -> 
        main ((Utils.strToInt numberOfNodes), (Utils.strToInt numberOfRequests))
    | _ -> printfn "Error: Invalid Arguments"