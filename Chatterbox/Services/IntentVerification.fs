namespace Chatterbox.Services

open System
open System.Net
open System.Threading.Tasks
open Chatterbox.Data
open Chatterbox.Data.``public``
open Chatterbox.Requests
open FsHttp

[<RequireQualifiedAccess>]
module IntentVerification =
    let private is2xx (code: HttpStatusCode) =
        let n = int code
        n >= 200 && n < 300

    let verify (mode: Subscriptions.RequestMode) (subscription: subscriptions) : Task<Result<unit, string>> =
        task {
            let challenge = Guid.CreateVersion7().ToString("N")
            let! response = subscription |> (IntentVerificationRequest.build mode challenge >> Request.sendTAsync)
            let! body = response.content.ReadAsStringAsync()

            // https://www.w3.org/TR/websub/#verification-details
            return
                match (response.statusCode, body) with
                | code, text when is2xx code && text = challenge -> Ok()
                | code, _ when is2xx code -> Error "Response body did not match challenge."
                | HttpStatusCode.NotFound, _ -> Error "Subscriber rejected subscription."
                | code, _ -> Error $"Unexpected response code: {int code}."
        }
