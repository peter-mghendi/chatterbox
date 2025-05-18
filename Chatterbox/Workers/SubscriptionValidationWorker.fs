namespace Chatterbox.Workers

open Chatterbox.Services

type Validation = Validate of int64 * AsyncReplyChannel<Result<unit, string option>>

[<RequireQualifiedAccess>]
module SubscriptionValidationWorker =
    let agent =
        MailboxProcessor<Validation>.Start(fun inbox ->
            let rec loop () =
                async {
                    let! Validate (id, reply) = inbox.Receive()
                    let! subscription = Subscriptions.find id |> Async.AwaitTask
                    
                    reply.Reply (SubscriptionValidation.validate subscription)
                    return! loop ()
                }

            loop ()
        )