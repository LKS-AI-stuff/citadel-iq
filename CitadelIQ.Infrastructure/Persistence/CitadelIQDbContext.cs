using CitadelIQ.Application.Options;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Infrastructure.Persistence;

public class CitadelIQDbContext(DbContextOptions<CitadelIQDbContext> options, IOptions<OpenAIOptions> openAIOptions)
    : DbContext(options)
{
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The schema is owned by the CitadelIQ.FluentMigrations project; these mappings only
        // describe it to EF Core for querying.
        modelBuilder.ApplyConfiguration(new FolderConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentChunkConfiguration(openAIOptions.Value.EmbeddingDimension));
    }
}
