namespace Chatterbox.Workers

open System
open Chatterbox.Data.``public``
open Chatterbox.Services
open type Chatterbox.Data.``public``.subscription_status

type WorkflowMessage =
    | StartWorkflow of Subscriptions.RequestMode * int64
    | ValidationResult of int64 * Result<unit, string option>
    | VerificationResult of Subscriptions.RequestMode * int64 * Result<unit, string>

module SubscriptionWorkflowOrchestrator =
    let orchestrator (validator: MailboxProcessor<_>) (verifier: MailboxProcessor<_>) =
        MailboxProcessor.Start(fun inbox ->
            let rec loop () =
                async {
                    match! inbox.Receive() with
                    | StartWorkflow(Subscriptions.Subscribe, id) ->
                        async {
                            let! result = validator.PostAndAsyncReply(fun reply -> Validate(id, reply))
                            inbox.Post(ValidationResult(id, result))
                        }
                        |> Async.Start

                        return! loop ()
                    | StartWorkflow(mode as Subscriptions.Unsubscribe, id) ->
                        async {
                            let! result = verifier.PostAndAsyncReply(fun reply -> Verify(mode, id, reply))
                            inbox.Post(VerificationResult(mode, id, result))
                        }
                        |> Async.Start

                        return! loop ()
                    | ValidationResult(id, Ok _) ->
                        let mode = Subscriptions.Subscribe
                        let! subscription = Subscriptions.find id |> Async.AwaitTask
                        do! Subscriptions.approve subscription |> Async.AwaitTask

                        async {
                            let! result = verifier.PostAndAsyncReply(fun reply -> Verify(mode, id, reply))
                            inbox.Post(VerificationResult(mode, id, result))
                        }
                        |> Async.Start

                        return! loop ()
                    | ValidationResult(id, Error reason) ->
                        let! subscription = Subscriptions.find id |> Async.AwaitTask
                        do! Subscriptions.deny subscription |> Async.AwaitTask
                        do! SubscriptionNotification.deny reason subscription |> Async.AwaitTask

                        return! loop ()
                    | VerificationResult(Subscriptions.Subscribe, id, Ok _) ->
                        let! subscription = Subscriptions.find id |> Async.AwaitTask
                        match!
                            Subscriptions.lookup [ active ] (Uri subscription.callback, Uri subscription.topic)
                            |> Async.AwaitTask
                        with
                        | Some s -> do! Subscriptions.delete s |> Async.AwaitTask
                        | None -> ()

                        do! Subscriptions.activate subscription |> Async.AwaitTask

                        return! loop ()
                    | VerificationResult(Subscriptions.Unsubscribe, id, Ok _) ->
                        let! subscription = Subscriptions.find id |> Async.AwaitTask
                        do! Subscriptions.delete subscription |> Async.AwaitTask

                        return! loop ()
                    | VerificationResult(Subscriptions.Subscribe, id, Error _) ->
                        let! subscription = Subscriptions.find id |> Async.AwaitTask
                        do! Subscriptions.reject subscription |> Async.AwaitTask

                        return! loop ()
                    | VerificationResult(Subscriptions.Unsubscribe, _, Error _) ->
                        return! loop ()
                }

            loop ())
