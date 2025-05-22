namespace Chatterbox.Services

open System.Threading.Tasks
open Chatterbox.Services
open Chatterbox.Workers
open type Chatterbox.Data.``public``.subscription_status

module SubscriptionService =
    let unsubscribe (request: Subscriptions.Request) (orchestrator: MailboxProcessor<WorkflowMessage>) : Task<Result<unit, string>> =
        task {
            match! Subscriptions.lookup [ active ] (request.Callback, request.Topic) with
            | Some subscription -> orchestrator.Post (StartWorkflow (Subscriptions.Unsubscribe, subscription.id))
            | None -> ()
            
            return Ok ()
        }

    let subscribe (request: Subscriptions.Request) (orchestrator: MailboxProcessor<WorkflowMessage>) : Task<Result<unit, string>> =
        task {
            match! Subscriptions.lookup [ pending ] (request.Callback, request.Topic) with
            | Some subscription -> do! Subscriptions.delete subscription
            | None -> ()
            
            let! id = Subscriptions.create request
            orchestrator.Post (StartWorkflow(Subscriptions.Subscribe, id))
            return Ok ()
        }
