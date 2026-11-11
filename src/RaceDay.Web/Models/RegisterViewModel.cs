using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public sealed class RegisterViewModel
{
    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    [Display(Name = "Password (at least 8 characters)")]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Participant";
}
