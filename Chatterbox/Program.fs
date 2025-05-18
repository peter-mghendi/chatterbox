open System.Net
open Chatterbox.Handlers
open Chatterbox.Workers
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection

let builder = WebApplication.CreateBuilder()

let validator = SubscriptionValidationWorker.agent
let verifier = IntentVerificationWorker.agent

builder.Services.AddKeyedSingleton<MailboxProcessor<Validation>>("validator", validator)
|> ignore

builder.Services.AddKeyedSingleton<MailboxProcessor<Verification>>("verifier", verifier)
|> ignore

builder.Services.AddKeyedSingleton<MailboxProcessor<WorkflowMessage>>(
    "orchestrator",
    SubscriptionWorkflowOrchestrator.orchestrator validator verifier
)
|> ignore

let wapp = builder.Build()

wapp
    .UseRouting()
    .UseFalco([ post "/hub" HubHandler.handle ])
    .Run(Response.withStatusCode (int HttpStatusCode.NotFound) >> Response.ofEmpty)
