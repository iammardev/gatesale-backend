using System.ComponentModel.DataAnnotations;

namespace GateSale.Core.Entities
{
    public class WhitelistedDomain
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(255)]
        public string Domain { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string SchoolName { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
    }
}