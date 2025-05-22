namespace Chatterbox.Workers

open Chatterbox.Services

type Verification = Verify of Subscriptions.RequestMode * int64 * AsyncReplyChannel<Result<unit, string>>

[<RequireQualifiedAccess>]  
module IntentVerificationWorker =

    let agent =
        MailboxProcessor<Verification>.Start(fun inbox ->
            let rec loop () =
                async {
                    let! Verify (mode, id, reply) = inbox.Receive()
                    let! subscription = Subscriptions.find id |> Async.AwaitTask
                    let! result = IntentVerification.verify mode subscription |> Async.AwaitTask
                    
                    reply.Reply result
                    return! loop ()
                }

            loop()
        )