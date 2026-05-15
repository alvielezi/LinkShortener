using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SendGrid;
using SendGrid.Helpers.Mail;
using ShortlyClient.Data.ViewModels;
using ShortlyClient.Helpers.Roles;
using ShortlyData.Models;
using Shortly.Data.Services;
using System.Security.Claims;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Microsoft.Extensions.Logging;

namespace ShortlyClient.Controllers
{
	public class AuthenticationController : Controller
	{
		private IUsersService _usersService;
		private SignInManager<AppUser> _signInManager;
		private UserManager<AppUser> _userManager;
		private IConfiguration _configuration;
		private readonly ILogger<AuthenticationController> _logger;

		public AuthenticationController(IUsersService usersService,
			SignInManager<AppUser> signInManager,
			UserManager<AppUser> userManager,
			IConfiguration configuration,
			ILogger<AuthenticationController> logger)
		{
			_usersService = usersService;
			_signInManager = signInManager;
			_userManager = userManager;
			_configuration = configuration;
			_logger = logger;
		}

		public async Task<IActionResult> Users()
		{
			var users = await _usersService.GetUsersAsync();
			return View(users);
		}

		public async Task<IActionResult> Login()
		{
			var loginVM = new LoginVM()
			{
				Schemes = await _signInManager.GetExternalAuthenticationSchemesAsync()
			};

			return View(loginVM);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> LoginSubmitted(LoginVM loginVM)		
		{
			if (!ModelState.IsValid)
			{
				loginVM.Schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
				return View("Login", loginVM);
			}

			var user = await _userManager.FindByEmailAsync(loginVM.EmailAddress);
			if (user == null)
			{
				loginVM.Schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
				ModelState.AddModelError("", "Invalid login attempt. Please, check your username and password");
				return View("Login", loginVM);
			}

			var userPasswordCheck = await _userManager.CheckPasswordAsync(user, loginVM.Password);
			if (!userPasswordCheck)
			{
				await _userManager.AccessFailedAsync(user);
				loginVM.Schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();

				if (await _userManager.IsLockedOutAsync(user))
				{
					ModelState.AddModelError("", "Your account is locked, please try again in 10 mins");
					return View("Login", loginVM);
				}

				ModelState.AddModelError("", "Invalid login attempt. Please, check your username and password");
				return View("Login", loginVM);
			}

			var userLoggedIn = await _signInManager.PasswordSignInAsync(user, loginVM.Password, false, false);

			if (userLoggedIn.Succeeded)
				return RedirectToAction("Index", "Home");
			else if (userLoggedIn.IsNotAllowed)
				return RedirectToAction("EmailConfirmation");
			else if (userLoggedIn.RequiresTwoFactor)
				return RedirectToAction("TwoFactorConfirmation", new { loggedInUserId = user.Id });

			loginVM.Schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
			ModelState.AddModelError("", "Invalid login attempt. Please, check your username and password");
			return View("Login", loginVM);
		}

		public async Task<IActionResult> Register()
		{
			return View(new RegisterVM());
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RegisterUser(RegisterVM registerVM)
		{
			if (!ModelState.IsValid)
			{
				return View("Register", registerVM);
			}

			var user = await _userManager.FindByEmailAsync(registerVM.EmailAddress);
			if (user != null)
			{
				ModelState.AddModelError("", "Email address is already in use.");
				return View("Register", registerVM);
			}

			var newUser = new AppUser()
			{
				Email = registerVM.EmailAddress,
				UserName = registerVM.EmailAddress,
				FullName = registerVM.FullName,
				LockoutEnabled = true
			};

			var userCreated = await _userManager.CreateAsync(newUser, registerVM.Password);
			if (userCreated.Succeeded)
			{
				await _userManager.AddToRoleAsync(newUser, Role.User);

				// Send confirmation but protect against missing SendGrid config
				try
				{
					return await SendEmailConfirmation(new ConfirmEmailLoginVM { EmailAddress = registerVM.EmailAddress });
				}
				catch (Exception ex)
				{
					// don't leak exceptions to user; log in real app
					TempData["EmailConfirmation"] = "Registration successful but confirmation email could not be sent. Contact admin.";
					return RedirectToAction("Index", "Home");
				}
			}

			foreach (var error in userCreated.Errors)
			{
				ModelState.AddModelError("", error.Description);
			}

			return View("Register", registerVM);
		}

		public async Task<IActionResult> Logout()
		{
			await _signInManager.SignOutAsync();
			return RedirectToAction("Index", "Home");
		}


		public async Task<IActionResult> EmailConfirmation()
		{
			var confirmEmail = new ConfirmEmailLoginVM();
			return View(confirmEmail);
		}

		public async Task<IActionResult> SendEmailConfirmation(ConfirmEmailLoginVM confirmEmailLoginVM)
		{
			//1. Check if the user exists
			var user = await _userManager.FindByEmailAsync(confirmEmailLoginVM.EmailAddress);

			//2. Create a confirmation link
			if (user != null)
			{
				var userToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

				// URL-encode token so it is safe in query string and won't break email clients
				var encodedToken = System.Net.WebUtility.UrlEncode(userToken);

				// Build absolute link and log it for debugging
				var userConfirmationLink = Url.Action("EmailConfirmationVerified", "Authentication",
					new { userId = user.Id, userConfirmationToken = encodedToken }, Request.Scheme);

				_logger.LogInformation("Generated email confirmation link for {Email}: {Link}", confirmEmailLoginVM.EmailAddress, userConfirmationLink);

				//3. Send the email
				var apiKey = _configuration["SendGrid:ShortlyKey"];
				if (string.IsNullOrWhiteSpace(apiKey))
				{
					_logger.LogWarning("SendGrid API key is missing.");
					TempData["EmailConfirmation"] = "Registration successful. Configure SendGrid to send confirmation emails.";
					return RedirectToAction("Index", "Home");
				}

				var sendGridClient = new SendGridClient(apiKey);
				var fromEmailAddress = new EmailAddress(_configuration["SendGrid:FromAddress"], "Shortly Client App");
				var emailSubject = "[Shortly] Verify your account";
				var toEmailAddress = new EmailAddress(confirmEmailLoginVM.EmailAddress);

				// Provide full absolute URL in both plain text and HTML; include an explicit pasteable URL line
				var emailContentTxt = $"Hello from Shortly App.\n\nPlease verify your account by visiting this link:\n{userConfirmationLink}\n\nIf the link is not clickable, copy and paste the URL into your browser.";
				var emailContentHtml = $@"
					<p>Hello from Shortly App.</p>
					<p>Please verify your account by clicking the link below:</p>
					<p><a href=""{userConfirmationLink}"">Verify your account</a></p>
					<p>If the link is not clickable, copy and paste this URL into your browser:</p>
					<p><a href=""{userConfirmationLink}"">{userConfirmationLink}</a></p>";

				var emailRequest = MailHelper.CreateSingleEmail(fromEmailAddress, toEmailAddress, emailSubject, emailContentTxt, emailContentHtml);

				try
				{
					var emailResponse = await sendGridClient.SendEmailAsync(emailRequest);

					var responseBody = string.Empty;
					try { responseBody = await emailResponse.Body.ReadAsStringAsync(); } catch { }

					_logger.LogInformation("SendGrid response Status={Status} for To={To}. Body={Body}", emailResponse.StatusCode, confirmEmailLoginVM.EmailAddress, responseBody);

					if (emailResponse.StatusCode == System.Net.HttpStatusCode.Accepted || emailResponse.StatusCode == System.Net.HttpStatusCode.OK)
					{
						TempData["EmailConfirmation"] = "Thank you! Please, check your email to verify your account (check spam/promotions).";
						_logger.LogInformation("SendGrid accepted email send request to {To}.", confirmEmailLoginVM.EmailAddress);
					}
					else
					{
						_logger.LogError("SendGrid returned non-success status {Status} for {To}. Body: {Body}", emailResponse.StatusCode, confirmEmailLoginVM.EmailAddress, responseBody);
						TempData["EmailConfirmation"] = "Registration succeeded but confirmation email was not sent (SendGrid error).";
					}

					return RedirectToAction("Index", "Home");
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Exception sending confirmation email to {Email}", confirmEmailLoginVM.EmailAddress);
					TempData["EmailConfirmation"] = "Registration succeeded but sending confirmation email failed. Check server logs.";
					return RedirectToAction("Index", "Home");
				}
			}

			ModelState.AddModelError("", $"Email address {confirmEmailLoginVM.EmailAddress} does not exist");
			return View("EmailConfirmation", confirmEmailLoginVM);
		}

		public async Task<IActionResult> EmailConfirmationVerified(string userId, string userConfirmationToken)
		{
			var user = await _userManager.FindByIdAsync(userId);

			if (user == null)
			{
				return RedirectToAction("Index", "Home");
			}

			var result = await _userManager.ConfirmEmailAsync(user, userConfirmationToken);

			TempData["EmailConfirmationVerified"] = "Thank you! Your account has been confirmed. You can now log in!";
			return RedirectToAction("Index", "Home");
		}

		public async Task<IActionResult> TwoFactorConfirmation(string loggedInUserId)
		{
			// 1. Get the user
			var user = await _userManager.FindByIdAsync(loggedInUserId);

			if (user != null)
			{
				var userToken = await _userManager.GenerateTwoFactorTokenAsync(user, "Phone");

				// 2. Send the SMS (set up twilio)
				string twilioPhoneNumber = _configuration["Twilio:PhoneNumber"];
				string twilioSID = _configuration["Twilio:SID"];
				string twilioToken = _configuration["Twilio:Token"];

				TwilioClient.Init(twilioSID, twilioToken);

				var message = MessageResource.Create(
						body: $"This is your verification code: {userToken}",
						from: new Twilio.Types.PhoneNumber(twilioPhoneNumber),
						to: new Twilio.Types.PhoneNumber(user.PhoneNumber)
					);

				var confirm2FALoginVM = new Confirm2FALoginVM()
				{
					UserId = loggedInUserId
				};

				return View(confirm2FALoginVM);

			}

			return RedirectToAction("Index", "Home");
		}

		public async Task<IActionResult> TwoFactorConfirmationVerified(Confirm2FALoginVM confirm2FALoginVM)
		{
			var user = await _userManager.FindByIdAsync(confirm2FALoginVM.UserId);

			if (user != null)
			{
				var tokenVerification = await _userManager.VerifyTwoFactorTokenAsync(user, "Phone", confirm2FALoginVM.UserConfirmationCode);

				if (tokenVerification)
				{
					var tokenSignIn = await _signInManager.TwoFactorSignInAsync("Phone", confirm2FALoginVM.UserConfirmationCode, false, false);

					if (tokenSignIn.Succeeded)
						return RedirectToAction("Index", "Home");
				}
			}

			ModelState.AddModelError("", "Confirmation code is not correct");
			return View(confirm2FALoginVM);
		}


		public IActionResult ExternalLogin(string provider, string returnUrl = "")
		{
			var redirectUrl = Url.Action("ExternalLoginCallback", "Authentication", new { ReturnUrl = returnUrl });

			var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);

			return new ChallengeResult(provider, properties);
		}

		public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "", string remoteError = "")
		{

			var loginVM = new LoginVM()
			{
				Schemes = await _signInManager.GetExternalAuthenticationSchemesAsync()
			};

			if (!string.IsNullOrEmpty(remoteError))
			{
				ModelState.AddModelError("", $"Error from extranal login provide: {remoteError}");
				return View("Login", loginVM);
			}

			//Get login info
			var info = await _signInManager.GetExternalLoginInfoAsync();
			if (info == null)
			{
				ModelState.AddModelError("", $"Error from extranal login provide: {remoteError}");
				return View("Login", loginVM);
			}

			var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

			if (signInResult.Succeeded)
				return RedirectToAction("Index", "Home");
			else
			{
				var userEmail = info.Principal.FindFirstValue(ClaimTypes.Email);
				if (!string.IsNullOrEmpty(userEmail))
				{
					var user = await _userManager.FindByEmailAsync(userEmail);

					if (user == null)
					{
						user = new AppUser()
						{
							UserName = userEmail,
							Email = userEmail,
							EmailConfirmed = true
						};

						await _userManager.CreateAsync(user);
						await _userManager.AddToRoleAsync(user, Role.User);
					}

					await _signInManager.SignInAsync(user, isPersistent: false);

					return RedirectToAction("Index", "Home");
				}

			}

			ModelState.AddModelError("", $"Something went wrong");
			return View("Login", loginVM);
		}
	}
}