namespace AkkaNeo_Blazor.Models
{
    public class GraphNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Label { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();

        // Visual properties for rendering
        public string Color { get; set; } = "#1f77b4";
        public int Size { get; set; } = 10;
    }
}
