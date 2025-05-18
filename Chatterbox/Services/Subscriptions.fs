namespace Chatterbox.Services

open System
open System.Threading.Tasks
open Chatterbox.Data.``public``
open Chatterbox.Data.``public``
open SqlHydra.Query
open Microsoft.FSharp.Core
open Chatterbox.Data.Context
open Chatterbox.Data.HydraBuilders

[<RequireQualifiedAccess>]
module Subscriptions =
    [<Literal>]
    let private DefaultLease = 7L * TimeSpan.SecondsPerDay

    type Request =
        { Callback: Uri
          Topic: Uri
          Lease: int64 option
          Secret: byte array option }

    type RequestMode =
        | Subscribe
        | Unsubscribe

    let private transition status subscription =
        match status with
        | subscription_status.active ->
            let expiry =
                DateTime.UtcNow + TimeSpan.FromSeconds subscription.granted_lease_seconds

            updateTask openContext {
                for s in subscriptions do
                    set s.status status
                    set s.expires_at expiry
                    where (s.id = subscription.id)
            }
        | _ ->
            updateTask openContext {
                for s in subscriptions do
                    set s.status status
                    where (s.id = subscription.id)
            }
        :> Task

    let create (request: Request) =
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

    let find id =
        selectTask openContext {
            for subscription in subscriptions do
                where (subscription.id = id)
                head
        }

    let lookup (statuses: subscription_status list) (callback: Uri, topic: Uri) =
        selectTask openContext {
            for subscription in subscriptions do
                where (
                    subscription.topic = topic.ToString()
                    && subscription.callback = callback.ToString()
                    && (isIn subscription.status statuses)
                )

                tryHead
        }

    let approve subscription = subscription |> transition subscription_status.approved

    let deny subscription = subscription |> transition subscription_status.denied

    let activate subscription = subscription |> transition subscription_status.active

    let reject subscription = subscription |> transition subscription_status.rejected

    let delete subscription = subscription |> transition subscription_status.deleted
