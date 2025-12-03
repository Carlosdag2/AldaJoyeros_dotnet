using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AldaJoyeros.Entities
{
    [Table("password_reset_tokens")]
    public class PasswordResetToken
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("user_id")]
        public long UserId { get; set; }

        [Required]
        [Column("token")]
        [StringLength(255)]
        public string Token { get; set; } = string.Empty;

        [Required]
        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("used")]
        public bool Used { get; set; } = false;

        [Required]
        [Column("code")]
        [StringLength(6)]
        public string Code { get; set; } = string.Empty;

        [Column("attempts")]
        public int Attempts { get; set; } = 0;

        [Column("code_verified")]
        public bool CodeVerified { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Relación
        [ForeignKey("UserId")]
        public virtual Usuario Usuario { get; set; } = null!;
    }
}
