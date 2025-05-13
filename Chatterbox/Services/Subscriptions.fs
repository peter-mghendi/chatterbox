namespace Chatterbox.Services

open System
open Chatterbox.Data
open Chatterbox.Data.Context
open SqlHydra.Query

module Subscriptions =
    [<Literal>]
    let DefaultLease = 7L * TimeSpan.SecondsPerDay
    
    type SubscriptionRequest =
        { Callback: Uri
          Topic: Uri
          Lease: int64 option
          Secret: byte array option }

    type SubscriptionRequestMode =
        | Subscribe
        | Unsubscribe

    let mutable subscriptions: SubscriptionRequest list = []

    let subscribe (request: SubscriptionRequest) : Result<int64, string> =
        let now = DateTime.UtcNow
        let grant = request.Lease |> Option.defaultValue DefaultLease
        
        insertTask openContext {
            for subscription in ``public``.subscriptions do
                entity
                    { ``public``.subscriptions.id = 0
                      ``public``.subscriptions.identifier = Guid.CreateVersion7()
                      ``public``.subscriptions.topic = request.Topic.ToString()
                      ``public``.subscriptions.callback = request.Callback.ToString()
                      ``public``.subscriptions.requested_lease_seconds = request.Lease
                      ``public``.subscriptions.granted_lease_seconds = grant
                      ``public``.subscriptions.secret = request.Secret
                      ``public``.subscriptions.status = ``public``.subscription_status.pending
                      ``public``.subscriptions.created_at = now
                      ``public``.subscriptions.expires_at = now + TimeSpan.FromSeconds grant }

                getId subscription.id
        }
        |> Async.AwaitTask
        |> Async.RunSynchronously
        |> Ok

    let unsubscribe (subscription: SubscriptionRequest) : Result<int64, string> =
        match
            (subscriptions
             |> List.tryFindIndex (fun x ->
                 x.Callback = subscription.Callback
                 && x.Topic = subscription.Topic
                 && x.Secret = subscription.Secret))
        with
        | Some index ->
            subscriptions <- subscriptions |> List.removeAt index
            Ok index
        | _ -> Ok -1    

