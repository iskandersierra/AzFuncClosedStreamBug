module Functions

open FSharp.Control.Tasks.V2
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Azure.WebJobs.Extensions.DurableTask
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Newtonsoft.Json
open System.IO
open System.Net
open System.Net.Http

[<Literal>]
let Orchestrate = "Orchestrate"

let readTextBody (request: HttpRequest) = task {
  use reader = new StreamReader(request.Body)
  return! reader.ReadToEndAsync()
}

let readJsonBody<'t> (request: HttpRequest) = task {
  let! text = readTextBody request
  return JsonConvert.DeserializeObject<'t> text
}

let readTextMessageBody (request: HttpRequestMessage) = task {
  use! stream = request.Content.ReadAsStreamAsync()
  use reader = new StreamReader(stream)
  return! reader.ReadToEndAsync()
}

let readJsonMessageBody<'t> (request: HttpRequestMessage) = task {
  let! text = readTextMessageBody request
  return JsonConvert.DeserializeObject<'t> text
}

[<FunctionName(Orchestrate)>]
let orchestrate ([<OrchestrationTrigger>] context: IDurableOrchestrationContext) =
  task {
    let input = context.GetInput<string>()

    return sprintf "Hello, %s" input
  }

[<FunctionName("StartWithMessage")>]
let startWithMessage 
  ([<HttpTrigger(AuthorizationLevel.Function, "post", Route = null)>] req: HttpRequestMessage)
  (logger: ILogger)
  ([<DurableClient>] client: IDurableOrchestrationClient) =
  task {
    try
      let! request = req |> readTextMessageBody

      let! instanceId = client.StartNewAsync("Orchestrate", null, request)

      return client.CreateCheckStatusResponse(req, instanceId)
      // return! durableClient.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, MaxResponseWait)
    with
    | exn ->
      logger.LogError(exn, exn.ToString())
      return req.CreateErrorResponse(HttpStatusCode.InternalServerError, exn)
  }


[<FunctionName("StartWithRequest")>]
let startWithRequest 
  ([<HttpTrigger(AuthorizationLevel.Function, "post", Route = null)>] req: HttpRequest)
  (logger: ILogger)
  ([<DurableClient>] client: IDurableOrchestrationClient) =
  task {
    try
      let! request = req |> readTextBody

      let! instanceId = client.StartNewAsync("Orchestrate", null, request)

      return client.CreateCheckStatusResponse(req, instanceId)
      // return! durableClient.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, MaxResponseWait)
    with
    | exn ->
      logger.LogError(exn, exn.ToString())
      return StatusCodeResult(500) :> IActionResult
  }

