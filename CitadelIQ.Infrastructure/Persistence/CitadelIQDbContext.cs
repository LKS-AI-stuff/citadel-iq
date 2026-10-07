using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Options;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Infrastructure.Persistence;

public class CitadelIQDbContext(
    DbContextOptions<CitadelIQDbContext> options,
    IOptions<OpenAIOptions> openAIOptions,
    IWorkspaceContext workspaceContext)
    : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<JoinRequest> JoinRequests => Set<JoinRequest>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    /// <summary>The scope's workspace. Read per query by the query filters below (EF parameterizes context members)
    /// and per connection by <see cref="WorkspaceConnectionInterceptor"/> for row-level security.</summary>
    public Guid? CurrentWorkspaceId => workspaceContext.WorkspaceId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The schema is owned by the CitadelIQ.FluentMigrations project; these mappings only
        // describe it to EF Core for querying.
        modelBuilder.ApplyConfiguration(new WorkspaceConfiguration());
        modelBuilder.ApplyConfiguration(new UserAccountConfiguration());
        modelBuilder.ApplyConfiguration(new MembershipConfiguration());
        modelBuilder.ApplyConfiguration(new JoinRequestConfiguration());
        modelBuilder.ApplyConfiguration(new FolderConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentChunkConfiguration(openAIOptions.Value.EmbeddingDimension));

        // Application-level workspace isolation (row-level security is the second, independent layer). With no
        // workspace the comparison matches nothing. Never call IgnoreQueryFilters() on these.
        modelBuilder.Entity<Folder>().HasQueryFilter(f => f.WorkspaceId == CurrentWorkspaceId);
        modelBuilder.Entity<Document>().HasQueryFilter(d => d.WorkspaceId == CurrentWorkspaceId);
        modelBuilder.Entity<DocumentChunk>().HasQueryFilter(c => c.WorkspaceId == CurrentWorkspaceId);
    }
}
