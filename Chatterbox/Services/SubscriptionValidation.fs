namespace Chatterbox.Services

open Chatterbox.Data
open Chatterbox.Data.``public``

[<RequireQualifiedAccess>]
module SubscriptionValidation =
    let validate (_: subscriptions) : Result<unit, string option> = Ok()
