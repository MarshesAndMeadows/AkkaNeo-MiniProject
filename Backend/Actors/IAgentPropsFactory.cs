using Akka.Actor;
using AkkaNeo_Blazor.Models;
using AkkaNeo_MiniProject.GraphServer.Actors;
using GraphServer.Neo4j;

namespace GraphServer.Actors
{
    public interface IAgentPropsFactory
    {
        Props Create(AgentInfo agentInfo, IGraphRepository graphRepository, AgentType agentType);
    }
}