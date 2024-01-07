using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace vgerard.scaleway.management
{
    public class StopVM
    {
        private readonly ILogger _logger;
        private static HttpClient sharedClient = new()
        {
            BaseAddress = new Uri("https://api.scaleway.com"),
        };

        public StopVM(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<StopVM>();
            sharedClient.DefaultRequestHeaders.Add("X-Auth-Token",
                                                System.Environment.GetEnvironmentVariable("SCW_API_KEY"));
        }

        private StringContent actionPOSTPayload(string action) {
            using StringContent jsonContent = new(JsonSerializer.Serialize(new
                {
                    action = action
                }),
                Encoding.UTF8,
                "application/json");
            return jsonContent;
        }

        [Function("StopVM")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", 
               Route="stop/{zone:regex(^[A-Z]{{2}}-[A-Z]{{3}}-\\d{{1}}$)}/{server_id:regex(^[\\dA-Z-]{{36}}$)}")] HttpRequestData req,
        string zone, string server_id)
        {
            _logger.LogInformation($"StopVM HTTP trigger for zone: {zone}, serverID: {server_id}");

            using HttpResponseMessage possibleActions = await sharedClient.GetAsync($"/instance/v1/zones/{zone}/servers/{server_id}/action");
            possibleActions.EnsureSuccessStatusCode();
            String? actions = await possibleActions.Content.ReadAsStringAsync();
            _logger.LogInformation($"{actions}");
/*
            using HttpResponseMessage standby_res = await sharedClient.PostAsync(
                "/instance/v1/zones/{param_zone}/servers/{param_server_id}/action",
                actionPOSTPayload("stop_in_place"));
*/

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Welcome to Azure Functions!");

            return response;
        }
    }
}
