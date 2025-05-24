using Microsoft.EntityFrameworkCore;

namespace AIModelIntegration.KnowledgeBaseAI.Contexts
{
    public class SourceDbContext(DbContextOptions<SourceDbContext> options) : DbContext(options)
    {
    }
}
