using ShortlyClient.Helpers.Validators;
using System.ComponentModel.DataAnnotations;

namespace ShortlyClient.Data.ViewModels
{
	public class ConfirmEmailLoginVM
	{
		[Required(ErrorMessage = "Email address is required")]
		[CustomEmailValidator(ErrorMessage = "Email address is not valid (custom)")]
		public string EmailAddress { get; set; }
	}
}