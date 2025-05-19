using Akka.Actor;
using AkkaNeo_Blazor.Models;
using GraphServer.Neo4j;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GraphServer.Neo4j
{
    public interface IGraphRepository
    {
        Task<bool> IsConnectedAsync();
        Task<(int nodeCount, int relationshipCount)> GetDatabaseSizeAsync();
        Task<List<GraphNode>> GetAllNodesAsync();
        Task<List<GraphRelationship>> GetAllRelationshipsAsync();
        Task<GraphNode> CreateNodeAsync(GraphNode node);
        Task<GraphRelationship> CreateRelationshipAsync(GraphRelationship relationship);
        Task<bool> DeleteNodeAsync(string nodeId);
        Task<bool> DeleteRelationshipAsync(string relationshipId);
        Task CreateSampleDataAsync();
        Task ClearDatabaseAsync();

        // Methods specific to Agent nodes
        Task<long> CreateAgentNodeAsync(string agentId, string name, string type, List<string> labels, Dictionary<string, object> properties);
        Task<long> GetAgentNodeIdAsync(string agentId);
        Task UpdateNodePropertyAsync(long nodeId, string propertyName, object propertyValue);
        Task DeleteAgentNodeAsync(string agentId);

        // Method for logging messages (if you intend to store them as nodes)
        Task CreateMessageNodeAsync(string senderId, string receiverId, string messageType, string content);
    }
}
