using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace pentyflixApi.Models.UserModel
{
    public class RegisterModel
    {
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}