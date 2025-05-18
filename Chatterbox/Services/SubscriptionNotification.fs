namespace Chatterbox.Services

open Chatterbox.Requests
open FsHttp

[<RequireQualifiedAccess>]
module SubscriptionNotification =
    let deny reason subscription =
        task {
            let! _ = subscription |> (SubscriptionDenialRequest.build reason >> Request.sendTAsync)
            return ()
        }     
