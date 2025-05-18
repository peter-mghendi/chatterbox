namespace Chatterbox.Requests

open Chatterbox.Data.``public``
open FsHttp

// https://www.w3.org/TR/websub/#subscription-validation
module SubscriptionDenialRequest =
    let build(reason: string option) (subscription: subscriptions) =
        http {
            GET subscription.callback
            query [
                "hub.mode", "denied"
                "hub.topic", subscription.topic
                yield! reason |> Option.toList |> List.map (fun r -> "hub.reason", r)
            ]
        }
