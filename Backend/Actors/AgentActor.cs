using Akka.Actor;
using GraphServer.Neo4j;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AkkaNeo_Blazor.Models;

namespace GraphServer.Actors
{
    // Messages for our actors
    public class StartAgent { }
    public class StopAgent { }
    public class GetStatus { }
    public class ProcessMessage { public AgentMessage Message { get; set; } }
    public class PerformAction { public string ActionType { get; set; } }

    // AgentActor - Base actor for different types of agents
    public class AgentActor : ReceiveActor, IWithTimers
    {
        private readonly AgentInfo _agentInfo;
        private readonly GraphRepository _graphRepository;
        private readonly IActorRef _supervisor;
        private int _messageCount = 0;

        public ITimerScheduler Timers { get; set; }

        public AgentActor(AgentInfo agentInfo, GraphRepository graphRepository, IActorRef supervisor)
        {
            _agentInfo = agentInfo;
            _graphRepository = graphRepository;
            _supervisor = supervisor;

            // Initial state - inactive
            Become(Inactive);

            // If agent is set to be active initially, start it
            if (_agentInfo.IsActive)
            {
                Self.Tell(new StartAgent());
            }
        }

        private void Active()
        {
            Receive<StopAgent>(_ =>
            {
                _agentInfo.IsActive = false;
                _agentInfo.Status = "Inactive";
                Timers.Cancel("heartbeat");
                Become(Inactive);

                // Notify supervisor
                _supervisor.Tell(new AgentStateChanged(_agentInfo.Id, false));
            });

            Receive<GetStatus>(_ =>
            {
                _agentInfo.LastActiveAt = DateTime.UtcNow;
                Sender.Tell(_agentInfo);
            });

            Receive<ProcessMessage>(msg =>
            {
                _messageCount++;
                _agentInfo.MessagesProcessed = _messageCount;
                _agentInfo.LastActiveAt = DateTime.UtcNow;
                _agentInfo.Status = "Processing";

                // Process message based on type and agent role
                ProcessMessageByType(msg.Message);

                // Update status
                _agentInfo.Status = "Active";

                // Notify supervisor
                _supervisor.Tell(new MessageProcessed(_agentInfo.Id, msg.Message.Id));
            });

            Receive<PerformAction>(act =>
            {
                _agentInfo.LastActiveAt = DateTime.UtcNow;
                _agentInfo.Status = "Working";

                // Perform action based on agent role
                PerformActionByType(act.ActionType);

                // Update status
                _agentInfo.Status = "Active";
            });

            Receive<HeartbeatTick>(_ =>
            {
                // Perform regular work
                Self.Tell(new PerformAction { ActionType = "Routine" });
            });
        }

        private void Inactive()
        {
            Receive<StartAgent>(_ =>
            {
                _agentInfo.IsActive = true;
                _agentInfo.Status = "Active";
                _agentInfo.LastActiveAt = DateTime.UtcNow;

                // Schedule heartbeat for regular activities
                Timers.StartPeriodicTimer("heartbeat", new HeartbeatTick(), TimeSpan.FromSeconds(30));

                Become(Active);

                // Notify supervisor
                _supervisor.Tell(new AgentStateChanged(_agentInfo.Id, true));
            });

            Receive<GetStatus>(_ =>
            {
                Sender.Tell(_agentInfo);
            });

            // All other messages are ignored in inactive state
            Receive<ProcessMessage>(_ =>
            {
                // Log or notify that agent is inactive
            });
        }

        private void ProcessMessageByType(AgentMessage message)
        {
            // Different handling based on agent type
            switch (_agentInfo.Type)
            {
                case "Processor":
                    ProcessorLogic(message);
                    break;
                case "Collector":
                    CollectorLogic(message);
                    break;
                case "Analyzer":
                    AnalyzerLogic(message);
                    break;
                case "Reporter":
                    ReporterLogic(message);
                    break;
            }
        }

        private void PerformActionByType(string actionType)
        {
            // Different actions based on agent type
            switch (_agentInfo.Type)
            {
                case "Processor":
                    // Process data in Neo4j
                    ProcessorRoutine();
                    break;
                case "Collector":
                    // Collect new data
                    CollectorRoutine();
                    break;
                case "Analyzer":
                    // Analyze existing data
                    AnalyzerRoutine();
                    break;
                case "Reporter":
                    // Report on findings
                    ReporterRoutine();
                    break;
            }
        }

        #region Agent Type Specific Logic

        private void ProcessorLogic(AgentMessage message)
        {
            // Example: Process a node creation command
            if (message.MessageType == "Command" && message.Content.ContainsKey("action") &&
                message.Content["action"].ToString() == "create_node")
            {
                // Create a node in Neo4j
                if (message.Content.ContainsKey("label") && message.Content.ContainsKey("properties"))
                {
                    var label = message.Content["label"].ToString();
                    var props = message.Content["properties"] as Dictionary<string, object>;

                    if (label != null && props != null)
                    {
                        var node = new GraphNode
                        {
                            Label = label,
                            Properties = props
                        };

                        // Asynchronously create the node
                        _ = _graphRepository.CreateNodeAsync(node);
                    }
                }
            }
        }

        private void CollectorLogic(AgentMessage message)
        {
            // Handle collector-specific messages
        }

        private void AnalyzerLogic(AgentMessage message)
        {
            // Handle analyzer-specific messages
        }

        private void ReporterLogic(AgentMessage message)
        {
            // Handle reporter-specific messages
        }

        private void ProcessorRoutine()
        {
            // Regular processor work
        }

        private void CollectorRoutine()
        {
            // Regular collector work
        }

        private void AnalyzerRoutine()
        {
            // Regular analyzer work
        }

        private void ReporterRoutine()
        {
            // Regular reporter work
        }

        #endregion
    }

    // Message classes
    public class HeartbeatTick { }
    public class AgentStateChanged
    {
        public string AgentId { get; }
        public bool IsActive { get; }

        public AgentStateChanged(string agentId, bool isActive)
        {
            AgentId = agentId;
            IsActive = isActive;
        }
    }

    public class MessageProcessed
    {
        public string AgentId { get; }
        public string MessageId { get; }

        public MessageProcessed(string agentId, string messageId)
        {
            AgentId = agentId;
            MessageId = messageId;
        }
    }
}
