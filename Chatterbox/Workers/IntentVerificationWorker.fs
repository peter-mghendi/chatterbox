namespace Chatterbox.Workers

open Chatterbox.Services

type Verification = Verify of int64 * Subscriptions.RequestMode * AsyncReplyChannel<Result<unit, string>>

[<RequireQualifiedAccess>]  
module IntentVerificationWorker =

    let agent =
        MailboxProcessor<Verification>.Start(fun inbox ->
            let rec loop () =
                async {
                    let! Verify (id, mode, reply) = inbox.Receive()
                    let! subscription = Subscriptions.find id |> Async.AwaitTask
                    let! result = IntentVerification.verify mode subscription |> Async.AwaitTask
                    
                    reply.Reply result
                    return! loop ()
                }

            loop()
        )