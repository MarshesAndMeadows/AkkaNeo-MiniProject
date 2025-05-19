namespace AkkaNeo_Blazor.Models
{
    public class AgentInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastActiveAt { get; set; }
        public int MessagesProcessed { get; set; }
        public string Status { get; set; } = "Idle";
        public Dictionary<string, object> Properties { get; set; } = new();
    }
}
