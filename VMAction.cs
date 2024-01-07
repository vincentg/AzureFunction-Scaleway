using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace vgerard.scaleway.management
{
    record ActionResponse {
        public IList<String>? actions {get; set;}
    }

    public class VMAction
    {
        private readonly ILogger _logger;
        private static HttpClient sharedClient = new()
        {
            BaseAddress = new Uri("https://api.scaleway.com"),
            DefaultRequestHeaders = {
                {"X-Auth-Token", Environment.GetEnvironmentVariable("SCW_API_KEY") ?? ""}
            }
        };

        public VMAction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<VMAction>();
        }

        [Function("Action")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", 
               Route="{action:maxlength(32)}/{zone:regex(^[A-Z]{{2}}-[A-Z]{{3}}-\\d{{1}}$)}/{server_id:guid}")] 
               HttpRequestData req, string action, string zone, string server_id, CancellationToken token)
        {
            _logger.LogInformation($"Executing {action} HTTP trigger for zone: {zone}, serverID: {server_id}");
            // Check if action is allowed
            using HttpResponseMessage possibleActions = await sharedClient.GetAsync($"/instance/v1/zones/{zone}/servers/{server_id}/action", token);
            possibleActions.EnsureSuccessStatusCode();

            var actionsResponse = await possibleActions.Content.ReadFromJsonAsync<ActionResponse>(token);
            bool allowed =  actionsResponse?.actions?.Contains(action) ?? false;
            _logger.LogInformation($"Is action {action} Allowed: {allowed}");

            // Execute action will throw exceptions on failures and non 200 status code of any subreqs
            if (allowed) {
                 _logger.LogInformation($"Executing action: {action}");
                // Server can be stopped, first do ACPI poweroff
                using StringContent jsonContent = new(JsonSerializer.Serialize(new
                {
                    action = action
                }),
                Encoding.UTF8,
                "application/json");

                using HttpResponseMessage standby_res = await sharedClient.PostAsync($"/instance/v1/zones/{zone}/servers/{server_id}/action",
                      jsonContent, token);
                standby_res.EnsureSuccessStatusCode();
                _logger.LogInformation($"Action: {action} Executed succesfully");
            } 

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString("Success!");
            return response;
        }
    }
}
