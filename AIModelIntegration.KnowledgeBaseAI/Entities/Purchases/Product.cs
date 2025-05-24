using AIModelIntegration.KnowledgeBaseAI.Entities.BaseEntities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AIModelIntegration.KnowledgeBaseAI.Entities.Purchases
{
    public class Product : BaseEntity
    {
        public int? CompanyId { get; set; }
        [MaxLength(255)]
        public string? CompanyName { get; set; }
        public int? AssetCategoryId { get; set; }
        [MaxLength(255)]
        public string? AssetCategoryName { get; set; }
        [Required, MaxLength(100)]
        public string ProductCode { get; set; }
        [Required, MaxLength(255)]
        public string ProductName { get; set; }
        [MaxLength(100)]
        public string ProductType { get; set; }
        [Precision(18, 6)]
        public decimal? Cost { get; set; }
        [Precision(18, 6)]
        public decimal? PurchasePrice { get; set; }
        [Precision(18, 6)]
        public decimal? SellingPrice { get; set; }
        [Precision(18, 6)]
        public decimal? PurchaseTax { get; set; }
        [Precision(18, 6)]
        public decimal? SalesTax { get; set; }
        [Precision(18, 6)]
        public decimal? Discount { get; set; }
        [Required]
        public bool Status { get; set; } = true;
        public bool IsFixedAsset { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
    }
}
