using AkkaNeo_Blazor.Models;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GraphServer.Neo4j
{
    public class GraphRepository
    {
        private readonly Neo4jDriver _neo4jDriver;

        public GraphRepository(Neo4jDriver neo4jDriver)
        {
            _neo4jDriver = neo4jDriver;
        }

        public async Task<bool> IsConnectedAsync()
        {
            return await _neo4jDriver.VerifyConnectivityAsync();
        }

        public async Task<(int nodeCount, int relationshipCount)> GetDatabaseSizeAsync()
        {
            using var session = _neo4jDriver.CreateAsyncSession();

            // Get node count
            var nodeCountResult = await session.RunAsync("MATCH (n) RETURN count(n) AS nodeCount");
            var nodeRecord = await nodeCountResult.SingleAsync();
            var nodeCount = nodeRecord["nodeCount"].As<int>();

            // Get relationship count
            var relCountResult = await session.RunAsync("MATCH ()-[r]->() RETURN count(r) AS relCount");
            var relRecord = await relCountResult.SingleAsync();
            var relCount = relRecord["relCount"].As<int>();

            return (nodeCount, relCount);
        }

        public async Task<List<GraphNode>> GetAllNodesAsync()
        {
            using var session = _neo4jDriver.CreateAsyncSession();
            var result = await session.RunAsync(@"
                MATCH (n) 
                RETURN id(n) AS id, labels(n) AS labels, properties(n) AS properties
            ");

            var nodes = new List<GraphNode>();
            var records = await result.ToListAsync();

            foreach (var record in records)
            {
                var nodeId = record["id"].As<string>();
                var labels = record["labels"].As<List<string>>();
                var properties = record["properties"].As<Dictionary<string, object>>();

                var node = new GraphNode
                {
                    Id = nodeId,
                    Label = labels.FirstOrDefault() ?? string.Empty,
                    Properties = properties
                };

                nodes.Add(node);
            }

            return nodes;
        }

        public async Task<List<GraphRelationship>> GetAllRelationshipsAsync()
        {
            using var session = _neo4jDriver.CreateAsyncSession();
            var result = await session.RunAsync(@"
                MATCH (source)-[r]->(target)
                RETURN id(r) AS id, type(r) AS type, id(source) AS sourceId, id(target) AS targetId, properties(r) AS properties
            ");

            var relationships = new List<GraphRelationship>();
            var records = await result.ToListAsync();

            foreach (var record in records)
            {
                var relId = record["id"].As<string>();
                var type = record["type"].As<string>();
                var sourceId = record["sourceId"].As<string>();
                var targetId = record["targetId"].As<string>();
                var properties = record["properties"].As<Dictionary<string, object>>();

                var relationship = new GraphRelationship
                {
                    Id = relId,
                    Type = type,
                    SourceNodeId = sourceId,
                    TargetNodeId = targetId,
                    Properties = properties
                };

                relationships.Add(relationship);
            }

            return relationships;
        }

        public async Task<GraphNode> CreateNodeAsync(GraphNode node)
        {
            using var session = _neo4jDriver.CreateAsyncSession();

            // Build query parameters from the node's properties
            var parameters = new Dictionary<string, object> { { "props", node.Properties } };

            // Create Cypher query
            var query = $"CREATE (n:{node.Label} $props) RETURN id(n) AS id";

            var result = await session.RunAsync(query, parameters);
            var record = await result.SingleAsync();

            // Update the node's ID
            node.Id = record["id"].As<string>();

            return node;
        }

        public async Task<GraphRelationship> CreateRelationshipAsync(GraphRelationship relationship)
        {
            using var session = _neo4jDriver.CreateAsyncSession();

            var parameters = new Dictionary<string, object>
            {
                { "sourceId", relationship.SourceNodeId },
                { "targetId", relationship.TargetNodeId },
                { "props", relationship.Properties }
            };

            var query = @"
                MATCH (source), (target)
                WHERE id(source) = $sourceId AND id(target) = $targetId
                CREATE (source)-[r:" + relationship.Type + " $props]->(target)
                RETURN id(r) AS id";


            var result = await session.RunAsync(query, parameters);
            var record = await result.SingleAsync();

            // Update the relationship's ID
            relationship.Id = record["id"].As<string>();

            return relationship;
        }

        public async Task<bool> DeleteNodeAsync(string nodeId)
        {
            using var session = _neo4jDriver.CreateAsyncSession();

            var parameters = new Dictionary<string, object> { { "id", nodeId } };
            var query = "MATCH (n) WHERE id(n) = $id DETACH DELETE n";

            await session.RunAsync(query, parameters);
            return true;
        }

        public async Task<bool> DeleteRelationshipAsync(string relationshipId)
        {
            using var session = _neo4jDriver.CreateAsyncSession();

            var parameters = new Dictionary<string, object> { { "id", relationshipId } };
            var query = "MATCH ()-[r]->() WHERE id(r) = $id DELETE r";

            await session.RunAsync(query, parameters);
            return true;
        }

        public async Task CreateSampleDataAsync()
        {
            using var session = _neo4jDriver.CreateAsyncSession();

            // Clear existing data
            await session.RunAsync("MATCH (n) DETACH DELETE n");

            // Create sample nodes and relationships
            var query = @"
                CREATE (alice:Person {name: 'Alice', age: 30})
                CREATE (bob:Person {name: 'Bob', age: 35})
                CREATE (charlie:Person {name: 'Charlie', age: 40})
                CREATE (databaseA:Database {name: 'GraphDB', type: 'Graph'})
                CREATE (databaseB:Database {name: 'RelationalDB', type: 'SQL'})
                CREATE (alice)-[:KNOWS {since: 2020}]->(bob)
                CREATE (bob)-[:KNOWS {since: 2018}]->(charlie)
                CREATE (charlie)-[:KNOWS {since: 2019}]->(alice)
                CREATE (alice)-[:USES {proficiency: 'Expert'}]->(databaseA)
                CREATE (bob)-[:USES {proficiency: 'Intermediate'}]->(databaseA)
                CREATE (charlie)-[:USES {proficiency: 'Beginner'}]->(databaseB)
            ";

            await session.RunAsync(query);
        }

        public async Task ClearDatabaseAsync()
        {
            using var session = _neo4jDriver.CreateAsyncSession();
            await session.RunAsync("MATCH (n) DETACH DELETE n");
        }
    }
}
