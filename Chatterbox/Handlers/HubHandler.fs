namespace Chatterbox.Handlers

open System
open System.Net
open System.Text
open Chatterbox.Data
open Chatterbox.Data.Context
open Falco
open SqlHydra.Query

module HubHandler =
    let DefaultLease = 7L * TimeSpan.SecondsPerDay

    type Subscription =
        { Callback: Uri
          Topic: Uri
          Lease: int64 option
          Secret: byte array option }

    let mutable subscriptions: Subscription list = []

    let subscribe (request: Subscription) =
        let grant =
            match request.Lease with
            | Some n -> n
            | None -> DefaultLease

        let now = DateTime.UtcNow
        let expiry = now + TimeSpan.FromSeconds(grant)

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
                      ``public``.subscriptions.expires_at = expiry }

                getId subscription.id
        }
        |> Async.AwaitTask
        |> Async.RunSynchronously
        |> fun id ->
            id |> printfn "subscription %d created"
            id
        |> Ok

    let unsubscribe (subscription: Subscription) =
        match
            (subscriptions
             |> List.tryFindIndex (fun x ->
                 x.Callback = subscription.Callback
                 && x.Topic = subscription.Topic
                 && x.Secret = subscription.Secret))
        with
        | Some index ->
            subscriptions <- subscriptions |> List.removeAt index
            index |> (int64 >> Ok)
        | _ -> 0 |> (int64 >> Ok)

    let handle: HttpHandler =
        Request.mapForm
            (fun f ->
                let hub =
                    match f.TryGet "hub" with
                    | Some j -> j
                    | None -> failwith "'hub' object missing in form"

                let callback =
                    match hub.TryGetStringNonEmpty "callback" with
                    | Some n -> Uri(n)
                    | None -> failwith "hub.callback is required"

                let topic =
                    match hub.TryGetStringNonEmpty "topic" with
                    | Some n -> Uri(n)
                    | None -> failwith "hub.topic is required"

                let lease =
                    hub.TryGetInt64 "lease_seconds"
                    |> Option.map (fun l ->
                        if l > 0L then
                            l
                        else
                            failwith "hub.lease_seconds must be positive")

                let secret =
                    hub.TryGetStringNonEmpty "secret"
                    |> Option.map (fun n ->
                        let bytes = Encoding.UTF8.GetBytes(n)

                        if bytes.Length < 200 then
                            bytes
                        else
                            failwith "hub.secret must be be less than 200 bytes in length.")

                let request =
                    { Callback = callback
                      Topic = topic
                      Lease = lease
                      Secret = secret }

                let result =
                    request
                    |> (fun r ->
                        r
                        |> match hub.GetStringNonEmpty "mode" with
                           | "subscribe" -> subscribe
                           | "unsubscribe" -> unsubscribe
                           | _ -> failwith "unknown mode")

                result)
            (fun _ -> Response.withStatusCode (int HttpStatusCode.Accepted) >> Response.ofEmpty)
