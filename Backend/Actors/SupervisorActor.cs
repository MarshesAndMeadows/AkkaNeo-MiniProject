
using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using GraphServer.Actors;
using AkkaNeo_Blazor.Models;
using GraphServer.Neo4j;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AkkaNeo_MiniProject.GraphServer.Actors
{
    /// <summary>
    /// SupervisorActor is responsible for creating, monitoring, and managing AgentActors
    /// It implements the supervision strategy for all agent actors in the system
    /// </summary>
    /// '
    public class AgentPropsFactory : IAgentPropsFactory
    {
        private readonly IGraphRepository _graphRepository;

        public AgentPropsFactory(IGraphRepository graphRepository)
        {
            _graphRepository = graphRepository;
        }

        public Props Create(AgentInfo agentInfo, AgentType agentType)
        {
            return agentType switch
            {
                AgentType.Processor => Props.Create(() => new AgentActor(agentInfo, _graphRepository, AgentType.Processor)),
                AgentType.Collector => Props.Create(() => new AgentActor(agentInfo, _graphRepository, AgentType.Collector)),
                AgentType.Analyzer => Props.Create(() => new AgentActor(agentInfo, _graphRepository, AgentType.Analyzer)),
                AgentType.Reporter => Props.Create(() => new AgentActor(agentInfo, _graphRepository, AgentType.Reporter)),
                AgentType.Aggregator => Props.Create(() => new AgentActor(agentInfo, _graphRepository, AgentType.Aggregator)),
                AgentType.Custom => Props.Create(() => new AgentActor(agentInfo, _graphRepository, AgentType.Custom)),
                _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, "Unsupported agent type")
            };
        }
    }
    public class SupervisorActor : ReceiveActor
    {
        private readonly IAgentPropsFactory _agentPropsFactory;
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private readonly Dictionary<string, IActorRef> _agents = new Dictionary<string, IActorRef>();
        private readonly IGraphRepository _graphRepository;
        private readonly IActorRef _graphHub;

        public SupervisorActor(IGraphRepository graphRepository, IActorRef graphHub, IAgentPropsFactory agentPropsFactory)
        {
            _graphRepository = graphRepository;
            _graphHub = graphHub;
            _agentPropsFactory = agentPropsFactory;

            // Message handlers for different agent management operations
            ReceiveAsync<Messages.CreateAgent>(HandleCreateAgent);
            ReceiveAsync<Messages.CreateAgent>(HandleCreateAgent);
            ReceiveAsync<Messages.StartAgent>(HandleStartAgent);
            ReceiveAsync<Messages.StopAgent>(HandleStopAgent);
            ReceiveAsync<Messages.DeleteAgent>(HandleDeleteAgent);
            Receive<Messages.GetAllAgents>(HandleGetAllAgents);
            ReceiveAsync<Messages.SendMessageToAgent>(HandleSendMessageToAgent);
            ReceiveAsync<Messages.AgentEvent>(HandleAgentEvent);
        }

        /// <summary>
        /// Creates the actor system's supervisory strategy for agent actors
        /// </summary>
        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(
                maxNrOfRetries: 10,
                withinTimeRange: TimeSpan.FromMinutes(10),
                decider: Decider.From(ex =>
                {
                    // Log the exception
                    _log.Error(ex, "Agent actor error");

                    // Decide how to handle different exceptions
                    switch (ex)
                    {
                        case ArgumentException _:
                            // Resume the actor for configuration errors
                            return Directive.Resume;

                        case InvalidOperationException _:
                            // Restart the actor for operational errors
                            return Directive.Restart;

                        case TimeoutException _:
                            // Restart for timeouts
                            return Directive.Restart;

                        default:
                            // For unknown exceptions, use a strategy based on severity
                            if (ex.Message.Contains("critical") || ex.Message.Contains("fatal"))
                                return Directive.Stop;
                            else
                                return Directive.Restart;
                    }
                }));
        }

        /// <summary>
        /// Creates a new agent actor and registers it in Neo4j if not already present
        /// </summary>
        /// 

        private async Task HandleCreateAgent(Messages.CreateAgent createMsg)
        {
            try
            {
                string agentId = !string.IsNullOrEmpty(createMsg.Agent.Id) ? createMsg.Agent.Id : Guid.NewGuid().ToString();
                createMsg.Agent.Id = agentId;

                if (_agents.ContainsKey(agentId))
                {
                    _log.Warning($"Agent with ID '{agentId}' already exists.");
                    Sender.Tell(new Messages.OperationFailed("CreateAgent", $"Agent with ID '{agentId}' already exists."));
                    return;
                }

                if (!Enum.TryParse<AgentType>(createMsg.Agent.Type, true, out var agentType))
                {
                    _log.Warning($"Invalid agent type: '{createMsg.Agent.Type}'. Creating as Custom.");
                    agentType = AgentType.Custom;
                }

                Props agentProps = _agentPropsFactory.Create(createMsg.Agent, agentType);

                var actorRef = Context.ActorOf(agentProps, $"agent-{agentId}");
                _agents[agentId] = actorRef;


                // Register agent in Neo4j if not already present
                if (!createMsg.AgentExistsInNeo4j)
                {
                    // Set default agent labels based on type
                    var labels = new List<string> { "Agent", createMsg.Agent.Type };

                    // Create node in Neo4j
                    long nodeId = await _graphRepository.CreateAgentNodeAsync(
                        agentId,
                        createMsg.Agent.Name,
                        createMsg.Agent.Type,
                        labels,
                        createMsg.Agent.Properties
                    );

                    // Update agent with Neo4j node ID
                    actorRef.Tell(new Messages.UpdateProperty("Neo4jNodeId", nodeId.ToString()));
                }

                // Start agent if requested
                if (createMsg.Agent.Status == "Active")
                {
                    actorRef.Tell(Messages.StartAgentSelf.Instance);
                }

                // Notify about successful creation
                _graphHub.Tell(new Messages.AgentEvent
                {
                    EventType = "Created",
                    AgentId = agentId,
                    AgentName = createMsg.Agent.Name,
                    Timestamp = DateTime.UtcNow
                });

                // Reply to sender
                Sender.Tell(new Messages.AgentCreated(agentId));
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to create agent: {0}", createMsg.Agent.Name);
                Sender.Tell(new Messages.OperationFailed("CreateAgent", ex.Message));
            }

        }


        /// <summary>
        /// Starts an existing agent
        /// </summary>
        private async Task HandleStartAgent(Messages.StartAgent startMsg)
        {
            try
            {
                if (!_agents.TryGetValue(startMsg.AgentId, out var actorRef))
                {
                    Sender.Tell(new Messages.OperationFailed("StartAgent", $"Agent not found: {startMsg.AgentId}"));
                    return;
                }

                // Tell agent to start
                actorRef.Tell(Messages.StartAgentSelf.Instance);

                // Update Neo4j status
                await UpdateAgentStatusInNeo4j(startMsg.AgentId, "Active");

                // Notify through hub
                _graphHub.Tell(new Messages.AgentEvent
                {
                    EventType = "Started",
                    AgentId = startMsg.AgentId,
                    Timestamp = DateTime.UtcNow
                });

                // Reply to sender
                Sender.Tell(new Messages.OperationSucceeded("StartAgent"));
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to start agent: {0}", startMsg.AgentId);
                Sender.Tell(new Messages.OperationFailed("StartAgent", ex.Message));
            }
        }

        /// <summary>
        /// Stops a running agent
        /// </summary>
        private async Task HandleStopAgent(Messages.StopAgent stopMsg)
        {
            try
            {
                if (!_agents.TryGetValue(stopMsg.AgentId, out var actorRef))
                {
                    Sender.Tell(new Messages.OperationFailed("StopAgent", $"Agent not found: {stopMsg.AgentId}"));
                    return;
                }

                // Tell agent to stop
                actorRef.Tell(Messages.StopAgentSelf.Instance);

                // Update Neo4j status
                await UpdateAgentStatusInNeo4j(stopMsg.AgentId, "Inactive");

                // Notify through hub
                _graphHub.Tell(new Messages.AgentEvent
                {
                    EventType = "Stopped",
                    AgentId = stopMsg.AgentId,
                    Timestamp = DateTime.UtcNow
                });

                // Reply to sender
                Sender.Tell(new Messages.OperationSucceeded("StopAgent"));
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to stop agent: {0}", stopMsg.AgentId);
                Sender.Tell(new Messages.OperationFailed("StopAgent", ex.Message));
            }
        }

        /// <summary>
        /// Deletes an agent by stopping it and removing it from the system
        /// </summary>
        private async Task HandleDeleteAgent(Messages.DeleteAgent deleteMsg)
        {
            try
            {
                if (!_agents.TryGetValue(deleteMsg.AgentId, out var actorRef))
                {
                    Sender.Tell(new Messages.OperationFailed("DeleteAgent", $"Agent not found: {deleteMsg.AgentId}"));
                    return;
                }

                // First tell agent to stop
                actorRef.Tell(Messages.StopAgentSelf.Instance);

                // Then stop the actor
                Context.Stop(actorRef);
                _agents.Remove(deleteMsg.AgentId);

                // Delete the agent from Neo4j if requested
                if (deleteMsg.DeleteFromNeo4j)
                {
                    await _graphRepository.DeleteAgentNodeAsync(deleteMsg.AgentId);
                }

                // Notify hub about deletion
                _graphHub.Tell(new Messages.AgentEvent
                {
                    EventType = "Deleted",
                    AgentId = deleteMsg.AgentId,
                    Timestamp = DateTime.UtcNow
                });

                // Reply to sender
                Sender.Tell(new Messages.OperationSucceeded("DeleteAgent"));
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to delete agent: {0}", deleteMsg.AgentId);
                Sender.Tell(new Messages.OperationFailed("DeleteAgent", ex.Message));
            }
        }

        /// <summary>
        /// Gets information about all known agents
        /// </summary>
        private void HandleGetAllAgents(Messages.GetAllAgents _)
        {
            var tasks = _agents.Select(async kvp =>
            {
                var state = await GetAgentStateAsync(kvp.Value);
                return state;
            }).ToList();

            Task.WhenAll(tasks).PipeTo(Sender);
        }

        /// <summary>
        /// Forwards a message to a specific agent
        /// </summary>
        private async Task HandleSendMessageToAgent(Messages.SendMessageToAgent sendMsg)
        {
            try
            {
                if (!_agents.TryGetValue(sendMsg.Message.ReceiverId, out var actorRef))
                {
                    Sender.Tell(new Messages.OperationFailed("SendMessageToAgent",
                        $"Agent not found: {sendMsg.Message.ReceiverId}"));
                    return;
                }

                // Forward the message to the target agent
                actorRef.Tell(sendMsg.Message);

                // Log message in Neo4j if requested
                if (sendMsg.LogInNeo4j)
                {
                    await _graphRepository.CreateMessageNodeAsync(
                        sendMsg.Message.SenderId,
                        sendMsg.Message.ReceiverId,
                        sendMsg.Message.MessageType,
                        sendMsg.Message.Content
                    );
                }

                // Notify hub about message
                _graphHub.Tell(new Messages.AgentEvent
                {
                    EventType = "MessageSent",
                    AgentId = sendMsg.Message.ReceiverId,
                    SourceId = sendMsg.Message.SenderId,
                    MessageType = sendMsg.Message.MessageType,
                });

                // Reply to sender
                Sender.Tell(new Messages.OperationSucceeded("SendMessageToAgent"));
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to send message to agent: {0}", sendMsg.Message.ReceiverId);
                Sender.Tell(new Messages.OperationFailed("SendMessageToAgent", ex.Message));
            }
        }

        /// <summary>
        /// Handles events from agents and forwards them to the GraphHub
        /// </summary>
        private Task HandleAgentEvent(Messages.AgentEvent eventMsg)
        {
            // Forward the event to the hub
            _graphHub.Tell(eventMsg);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the current state of an agent
        /// </summary>
        private async Task<AgentInfo> GetAgentStateAsync(IActorRef agentRef)
        {
            try
            {
                // Ask the agent for its state and await the response
                var state = await agentRef.Ask<AgentInfo>(Messages.GetAgentState.Instance,
                    TimeSpan.FromSeconds(5));
                return state;
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to get agent state: {0}", ex.Message);
                return new AgentInfo
                {
                    Id = "unknown",
                    Name = "Unknown",
                    Status = "Error",
                    LastActiveAt = DateTime.UtcNow,
                };
            }
        }

        /// <summary>
        /// Updates the agent status in Neo4j
        /// </summary>
        private async Task UpdateAgentStatusInNeo4j(string agentId, string status)
        {
            try
            {
                // Find the Neo4j node ID for this agent
                var nodeId = await _graphRepository.GetAgentNodeIdAsync(agentId);
                if (nodeId > 0)
                {
                    // Update the status property
                    await _graphRepository.UpdateNodePropertyAsync(nodeId, "status", status);
                }
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to update agent status in Neo4j: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Handle actor system termination
        /// </summary>
        protected override void PostStop()
        {
            _log.Info("SupervisorActor is stopping, shutting down all agents...");

            // Stop all agents gracefully
            foreach (var agent in _agents.Values)
            {
                agent.Tell(Messages.StopAgentSelf.Instance);
            }

            base.PostStop();
        }
    }

    /// <summary>
    /// Message types for Supervisor and Agent communication
    /// </summary>
    public static class Messages
    {
        // Agent management commands
        public class CreateAgent
        {
            public AgentInfo Agent { get; }
            public bool AgentExistsInNeo4j { get; }

            public CreateAgent(AgentInfo agent, bool agentExistsInNeo4j = false)
            {
                Agent = agent;
                AgentExistsInNeo4j = agentExistsInNeo4j;
            }
        }

        public class StartAgent
        {
            public string AgentId { get; }

            public StartAgent(string agentId)
            {
                AgentId = agentId;
            }
        }

        public class StopAgent
        {
            public string AgentId { get; }

            public StopAgent(string agentId)
            {
                AgentId = agentId;
            }
        }

        public class DeleteAgent
        {
            public string AgentId { get; }
            public bool DeleteFromNeo4j { get; }

            public DeleteAgent(string agentId, bool deleteFromNeo4j = true)
            {
                AgentId = agentId;
                DeleteFromNeo4j = deleteFromNeo4j;
            }
        }

        public class GetAllAgents
        {
            public static GetAllAgents Instance { get; } = new GetAllAgents();
            private GetAllAgents() { }
        }

        // Internal agent commands
        public class StartAgentSelf
        {
            public static StartAgentSelf Instance { get; } = new StartAgentSelf();
            private StartAgentSelf() { }
        }

        public class StopAgentSelf
        {
            public static StopAgentSelf Instance { get; } = new StopAgentSelf();
            private StopAgentSelf() { }
        }

        public class GetAgentState
        {
            public static GetAgentState Instance { get; } = new GetAgentState();
            private GetAgentState() { }
        }

        public class UpdateProperty
        {
            public string Key { get; }
            public string Value { get; }

            public UpdateProperty(string key, string value)
            {
                Key = key;
                Value = value;
            }
        }

        // Message passing
        public class SendMessageToAgent
        {
            public AgentMessage Message { get; }
            public bool LogInNeo4j { get; }

            public SendMessageToAgent(AgentMessage message, bool logInNeo4j = true)
            {
                Message = message;
                LogInNeo4j = logInNeo4j;
            }
        }

        // Events and responses
        public class AgentCreated
        {
            public string AgentId { get; }

            public AgentCreated(string agentId)
            {
                AgentId = agentId;
            }
        }

        public class OperationSucceeded
        {
            public string Operation { get; }

            public OperationSucceeded(string operation)
            {
                Operation = operation;
            }
        }

        public class OperationFailed
        {
            public string Operation { get; }
            public string Reason { get; }

            public OperationFailed(string operation, string reason)
            {
                Operation = operation;
                Reason = reason;
            }
        }

        public class AgentEvent
        {
            public string EventType { get; set; } = "";
            public string AgentId { get; set; } = "";
            public string AgentName { get; set; } = "";
            public string SourceId { get; set; } = "";
            public string MessageType { get; set; } = "";
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Enum defining the types of agents in the system
    /// </summary>
    public enum AgentType
    {
        Processor,
        Collector,
        Analyzer,
        Reporter,
        Aggregator,
        Custom
    }
}