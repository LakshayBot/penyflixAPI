using Microsoft.AspNetCore.Identity;

namespace pentyflixApi.Models.UserModel;
public class ApplicationUser : IdentityUser
{
    // Add any custom user properties here
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}