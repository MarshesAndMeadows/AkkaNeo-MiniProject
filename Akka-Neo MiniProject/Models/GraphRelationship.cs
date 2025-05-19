public class GraphRelationship
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();

    // Visual properties for rendering
    public string Color { get; set; } = "#aaa";
    public int Width { get; set; } = 1;
}