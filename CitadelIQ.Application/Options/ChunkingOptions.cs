namespace CitadelIQ.Application.Options;

public class ChunkingOptions
{
    public int ChunkSize { get; set; } = 800;

    public int ChunkOverlap { get; set; } = 150;
}
