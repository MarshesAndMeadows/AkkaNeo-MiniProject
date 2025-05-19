namespace AkkaNeo_Blazor.Models
{
    public class AgentMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SenderId { get; set; } = string.Empty;
        public string ReceiverId { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public Dictionary<string, object> Contents { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public string Status { get; set; } = "Pending";  
    }
}