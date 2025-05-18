namespace Chatterbox.Requests

open Chatterbox.Data.``public``
open Chatterbox.Services
open FsHttp

// https://www.w3.org/TR/websub/#hub-verifies-intent
module IntentVerificationRequest =
    let build (mode: Subscriptions.RequestMode) (challenge: string) (subscription: subscriptions) =
        http {
            GET subscription.callback

            query
                [ "hub.mode",
                  match mode with
                  | Subscriptions.Subscribe -> "subscribe"
                  | Subscriptions.Unsubscribe -> "unsubscribe"
                  "hub.topic", subscription.topic
                  "hub.challenge", challenge
                  "hub.lease_seconds", string subscription.granted_lease_seconds ]
        }
