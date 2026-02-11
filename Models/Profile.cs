using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwipeService.Models;

/// <summary>
/// Read-only profile entity for user ID to profile ID mapping
/// This is a view into user_service_db.Profiles for match validation
/// </summary>
[Table("Profiles", Schema = "dbo")]
public class Profile
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    // Other fields not needed for match validation
}
