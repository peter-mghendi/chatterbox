namespace Chatterbox.Handlers

open System
open System.Net
open System.Text
open Chatterbox.Common.Helpers
open Chatterbox.Services.Subscriptions
open Falco
open FsToolkit.ErrorHandling
open Microsoft.FSharp.Core.Operators

module HubHandler =
    type private ErrorResponse = { Error: string }

    [<Literal>]
    let private MaxSecretBytes = 200

    let private requireUri (key: string) (data: RequestData) =
        data.TryGetStringNonEmpty key
        |> Option.fold
            (fun _ str ->
                match Uri.TryCreate(str, UriKind.Absolute) with
                | true, uri when [Uri.UriSchemeHttp; Uri.UriSchemeHttps] |> List.contains uri.Scheme -> Ok uri
                | true,  _ -> Error $"'hub.{key}' must be a valid HTTP/HTTPS URI."
                | false, _ -> Error $"'hub.{key}' must be a valid URI.")
            (Error $"'hub.{key}' is required.")    
  
    let private getPositiveInt64 (key: string) (data: RequestData) =
        match data.TryGetInt64 key with
        | Some value when value > 0L -> Ok(Some value)
        | Some _ -> Error $"'hub.{key}' must be positive."
        | None -> Ok None

    let private getBytesOfMaxLength (length: int) (key: string) (data: RequestData) =
        data.TryGetStringNonEmpty key
        |> Option.map Encoding.UTF8.GetBytes
        |> Option.fold
            (fun _ bytes ->
                match bytes.Length with
                | n when n < length -> Ok(Some bytes)
                | _ -> Error $"'hub.{key}' must be be less than {length} bytes in length.")
            (Ok None)

    let private parse (f: RequestData) =
        result {
            let! hub =
                match f.TryGet "hub" with
                | Some h -> Ok h
                | None -> Error "'hub.' prefix must be present."

            let! mode =
                match hub.TryGetStringNonEmpty "mode" with
                | Some "subscribe" -> Ok Subscribe
                | Some "unsubscribe" -> Ok Unsubscribe
                | Some _ -> Error "'hub.mode' must be either 'subscribe' or 'unsubscribe'."
                | None -> Error "'hub.mode' is required."

            let! callback = hub |> requireUri "callback"
            let! topic = hub |> requireUri "topic"
            let! lease = hub |> getPositiveInt64 "lease_seconds"
            let! secret = hub |> getBytesOfMaxLength MaxSecretBytes "secret"

            return
                (mode,
                 { Callback = callback
                   Topic = topic
                   Lease = lease
                   Secret = secret })
        }

    let private dispatch =
        function
        | Subscribe -> subscribe
        | Unsubscribe -> unsubscribe

    let handle: HttpHandler =
        Request.mapForm
            (parse >> Result.bind (uncurry dispatch >> Async.AwaitTask >> Async.RunSynchronously))
            (function
            | Ok _ -> Response.withStatusCode (int HttpStatusCode.Accepted) >> Response.ofEmpty
            | Error e -> Response.withStatusCode (int HttpStatusCode.BadRequest) >> Response.ofJson { Error = e })
