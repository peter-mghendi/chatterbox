namespace Chatterbox.Services

open System
open System.Threading.Tasks
open SqlHydra.Query
open Microsoft.FSharp.Core
open Chatterbox.Common.Helpers
open Chatterbox.Data
open Chatterbox.Data.Context
open Chatterbox.Data.HydraBuilders
open Chatterbox.Data.``public``

module Subscriptions =
    [<Literal>]
    let private DefaultLease = 7L * TimeSpan.SecondsPerDay

    type SubscriptionRequest =
        { Callback: Uri
          Topic: Uri
          Lease: int64 option
          Secret: byte array option }

    type SubscriptionRequestMode =
        | Subscribe
        | Unsubscribe

    let private find (callback: Uri) (topic: Uri) =
        selectTask openContext {
            for subscription in subscriptions do
                where (
                    subscription.topic = topic.ToString()
                    && subscription.callback = callback.ToString()
                    && (isIn subscription.status [ subscription_status.pending; subscription_status.active ])
                )

                tryHead
        }

    let private create (request: SubscriptionRequest) =
        let now = DateTime.UtcNow
        let grant = request.Lease |> Option.defaultValue DefaultLease

        insertTask openContext {
            for subscription in subscriptions do
                entity
                    { subscriptions.id = 0
                      subscriptions.identifier = Guid.CreateVersion7()
                      subscriptions.topic = request.Topic.ToString()
                      subscriptions.callback = request.Callback.ToString()
                      subscriptions.requested_lease_seconds = request.Lease
                      subscriptions.granted_lease_seconds = grant
                      subscriptions.secret = request.Secret
                      subscriptions.status = subscription_status.pending
                      subscriptions.created_at = now
                      subscriptions.expires_at = now + TimeSpan.FromSeconds grant }

                getId subscription.id
        }

    let private delete (id: int64) =
        updateTask openContext {
            for subscription in subscriptions do
                set subscription.status subscription_status.deleted
                where (subscription.id = id)
        }

    let unsubscribe (request: SubscriptionRequest) : Task<Result<unit, string>> =
        task {
            let! existing = (request.Callback, request.Topic) |> uncurry find
            existing |> Option.iter (fun row -> delete row.id |> Async.AwaitTask |> ignore)
            return Ok()
        }

    let subscribe (request: SubscriptionRequest) : Task<Result<unit, string>> =
        task {
            let! result = request |> unsubscribe

            return
                result
                |> Result.bind (fun _ -> (create request) |> Async.AwaitTask |> Ok)
                |> Result.map (fun _ -> ())
        }
