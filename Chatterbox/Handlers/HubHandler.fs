namespace Chatterbox.Handlers

open System
open Falco

module HubHandler =
    type Subscription =
        { Callback: Uri
          Topic: string
          Lease: int option
          Secret: string option }

    let mutable subscriptions: Subscription list = []

    let subscribe (subscription: Subscription) =
        subscriptions <- subscription :: subscriptions
        Ok

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
            Ok
        | _ -> Ok

    let handle: HttpHandler =
        Request.mapForm (fun f ->
            let lease =
                match f.TryGetInt "hub.lease_seconds" with
                | Some n when n > 0 -> n
                | _ -> failwith "hub.lease_seconds must be positive"

            let request =
                { Callback = Uri(f.GetStringNonEmpty "hub.callback")
                  Topic = f.GetStringNonEmpty "hub.topic"
                  Lease = Some lease
                  Secret = f.TryGetString "hub.secret" }

            let result =
                request
                |> (fun r ->
                    r
                    |> match f.GetStringNonEmpty "hub.mode" with
                       | "subscribe" -> subscribe
                       | "unsubscribe" -> unsubscribe
                       | _ -> failwith "unknown mode")

            result |> ignore)
        |> ignore

        Response.ofJson (Response.withStatusCode 200 >> Response.ofEmpty)
