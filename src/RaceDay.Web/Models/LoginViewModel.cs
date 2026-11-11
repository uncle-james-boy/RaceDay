using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
