namespace Chatterbox.Workers

open System
open Chatterbox.Data.``public``
open Chatterbox.Services
open type Chatterbox.Data.``public``.subscription_status

type WorkflowMessage =
    | StartValidation of int64
    | ValidationResult of int64 * Result<unit, string option>
    | VerificationResult of int64 * Result<unit, string>

module SubscriptionWorkflowOrchestrator =
    let orchestrator (validator: MailboxProcessor<_>) (verifier: MailboxProcessor<_>) =
        MailboxProcessor.Start(fun inbox ->
            let rec loop () =
                async {
                    let! message = inbox.Receive()

                    match message with
                    | StartValidation id ->
                        async {
                            let! result = validator.PostAndAsyncReply(fun reply -> Validate(id, reply))
                            inbox.Post(ValidationResult(id, result))
                        }
                        |> Async.Start

                        return! loop ()
                    | ValidationResult(id, result) ->
                        let! subscription = Subscriptions.find id |> Async.AwaitTask

                        match result with
                        | Ok _ ->
                            do! Subscriptions.approve subscription |> Async.AwaitTask

                            async {
                                let! result =
                                    verifier.PostAndAsyncReply(fun reply -> Verify(id, Subscriptions.Subscribe, reply))

                                inbox.Post(VerificationResult(id, result))
                            }
                            |> Async.Start
                        | Error reason ->
                            do! Subscriptions.deny subscription |> Async.AwaitTask
                            do! SubscriptionNotification.deny reason subscription |> Async.AwaitTask

                        return! loop ()
                    | VerificationResult(id, result) ->
                        let! subscription = Subscriptions.find id |> Async.AwaitTask

                        match result with
                        | Ok _ ->
                            match!
                                Subscriptions.lookup [ active ] (Uri subscription.callback, Uri subscription.topic)
                                |> Async.AwaitTask
                            with
                            | Some s -> do! Subscriptions.delete s |> Async.AwaitTask
                            | None -> ()

                            do! Subscriptions.activate subscription |> Async.AwaitTask
                        | Error _ -> do! Subscriptions.reject subscription |> Async.AwaitTask

                        return! loop ()
                }

            loop ())
