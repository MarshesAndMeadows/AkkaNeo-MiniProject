using System.Net.Http.Json;
using AkkaNeo_Blazor.Models;

namespace AkkaNeo_Blazor.Services
{
    public class AkkaService
    {
        private readonly HttpClient _httpClient;

        public AkkaService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<AgentStatus> GetAgentStatusAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<AgentStatus>("api/agents/status")
                    ?? new AgentStatus { ActiveAgents = 0, MessagesProcessed = 0 };
            }
            catch (Exception)
            {
                return new AgentStatus { ActiveAgents = 0, MessagesProcessed = 0 };
            }
        }

        public async Task<List<AgentInfo>> GetAllAgentsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<AgentInfo>>("api/agents")
                    ?? new List<AgentInfo>();
            }
            catch (Exception)
            {
                return new List<AgentInfo>();
            }
        }

        public async Task<bool> StartAgentAsync(string agentId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/agents/{agentId}/start", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> StopAgentAsync(string agentId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/agents/{agentId}/stop", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SendMessageToAgentAsync(string agentId, AgentMessage message)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/agents/{agentId}/message", message);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> CreateAgentAsync(AgentInfo agent)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/agents", agent);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public class AgentStatus
    {
        public int ActiveAgents { get; set; }
        public int MessagesProcessed { get; set; }
    }
}
