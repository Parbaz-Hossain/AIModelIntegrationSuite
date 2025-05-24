using AIModelIntegration.KnowledgeBaseAI.Entities.Purchases;
using Microsoft.EntityFrameworkCore;

namespace AIModelIntegration.KnowledgeBaseAI.Contexts
{
    public class DestinationDbContext(DbContextOptions<DestinationDbContext> options) : DbContext(options)
    {
        public DbSet<Product> Products { get; set; }
    }
}
