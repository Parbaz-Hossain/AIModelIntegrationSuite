using System.ComponentModel.DataAnnotations;

namespace AIModelIntegration.KnowledgeBaseAI.Entities.BaseEntities
{
    public class BaseEntity
    {
        public int Id { get; set; }
        [Required]
        [StringLength(50)]
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; private set; } = DateTime.UtcNow;
        [StringLength(50)]
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
