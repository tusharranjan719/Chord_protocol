#r "nuget: Akka.FSharp"
namespace RequestType
open System.Collections.Generic
open Akka.FSharp
open Akka.Actor
module RequestType =
    type InitSupervisor = {
        NumberOfNodes: int;
        NumberOfRequests: int;
    }
    
    type InitChordMessage = {
        NumberOfNodes: int;
        NumberOfRequests: int;
        NodesArray: IActorRef array;
        // MaxNodes: int;
    }
    type InitNodeRequests = {
        NumberOfNodes: int;
        NumberOfRequests: int;
        NodesArray: IActorRef array;
        // MaxNodes: int;
    }
    type FindKeyMessage = {
        NodesArray: IActorRef array;
        RandomKey: int;
    }
    type RequestType =
        | InitSupervisor of InitSupervisor
        | StartStablization
        | TerminateChordRequest of int
        | InitChordMessage of InitChordMessage
        | InitChordInitiation
        | InitNodeRequests of InitNodeRequests
        | FixFingers of InitChordMessage
        | FindKey of int
        | FindKeyMessage of FindKeyMessage