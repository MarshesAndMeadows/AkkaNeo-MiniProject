using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AkkaNeo_Blazor.Models;

namespace AkkaNeo_Blazor.Services
{
    public class Neo4jService
    {
        private readonly HttpClient _httpClient;

        public Neo4jService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<DatabaseStatus> GetDatabaseStatusAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<DatabaseStatus>("api/graph/status")
                    ?? new DatabaseStatus { IsConnected = false, NodeCount = 0, RelationshipCount = 0 };
            }
            catch (Exception)
            {
                return new DatabaseStatus { IsConnected = false, NodeCount = 0, RelationshipCount = 0 };
            }
        }

        public async Task<List<GraphNode>> GetAllNodesAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<GraphNode>>("api/graph/nodes")
                    ?? new List<GraphNode>();
            }
            catch (Exception)
            {
                return new List<GraphNode>();
            }
        }

        public async Task<List<GraphRelationship>> GetAllRelationshipsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<GraphRelationship>>("api/graph/relationships")
                    ?? new List<GraphRelationship>();
            }
            catch (Exception)
            {
                return new List<GraphRelationship>();
            }
        }

        public async Task<bool> CreateSampleDataAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("api/graph/sample", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ClearDatabaseAsync()
        {
            try
            {
                var response = await _httpClient.DeleteAsync("api/graph/clear");
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<GraphNode> CreateNodeAsync(GraphNode node)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/graph/nodes", node);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<GraphNode>() ?? node;
                }
                return node;
            }
            catch (Exception)
            {
                return node;
            }
        }

        public async Task<GraphRelationship> CreateRelationshipAsync(GraphRelationship relationship)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/graph/relationships", relationship);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<GraphRelationship>() ?? relationship;
                }
                return relationship;
            }
            catch (Exception)
            {
                return relationship;
            }
        }
    }

    public class DatabaseStatus
    {
        public bool IsConnected { get; set; }
        public int NodeCount { get; set; }
        public int RelationshipCount { get; set; }
    }
}
