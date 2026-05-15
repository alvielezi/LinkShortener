using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using ShortlyClient.Helpers.Validators;

namespace ShortlyClient.Data.ViewModels
{
    public class LoginVM
    {
        [Required(ErrorMessage = "Email address is required")]
        [CustomEmailValidator(ErrorMessage = "Email address is not valid (custom)")]
        public string EmailAddress { get; set; }


        [Required(ErrorMessage = "Password is required")]
        [MinLength(5, ErrorMessage = "Password must be at least 5 characters")]
        public string Password { get; set; }

        public IEnumerable<AuthenticationScheme>? Schemes { get; set; }
    }
}