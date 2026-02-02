using System.ComponentModel.DataAnnotations;

namespace SwipeService.Models;

/// <summary>
/// Lightweight mapping between Keycloak user IDs and profile IDs
/// Populated when users swipe (we extract from JWT claims)
/// </summary>
public class UserProfileMapping
{
    [Key]
    public int ProfileId { get; set; }
    
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
