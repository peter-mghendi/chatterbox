open System.Net
open Chatterbox.Handlers
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder

let wapp = WebApplication.Create()

wapp
    .UseRouting()
    .UseFalco([ post "/hub" HubHandler.handle ])
    .Run(Response.withStatusCode (int HttpStatusCode.NotFound) >> Response.ofEmpty)
