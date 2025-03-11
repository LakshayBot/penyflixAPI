using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace pentyflixApi.Models.UserModel
{
    public class LoginModel
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}