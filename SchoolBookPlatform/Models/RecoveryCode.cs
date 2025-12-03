using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

public class RecoveryCode
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }

    [Required] 
    [Column(TypeName = "nvarchar(255)")]
    public string HashedCode { get; set; } = null!;
    
    public bool IsUsed { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } =  DateTime.Now.AddHours(7);
    public DateTime UsedAt { get; set; }

    [ForeignKey("UserId")] 
    public virtual User? User { get; set; } = null;
}