using Microsoft.AspNetCore.Mvc;
using ShortlyClient.Data.ViewModels;
using ShortlyData;
using ShortlyData.Models;
using System.Security.Claims;


namespace ShortlyClient.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private AppDbContext _context;

        public HomeController(ILogger<HomeController> logger, AppDbContext context)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var newUrl = new PostUrlVM();
            return View(newUrl);
        }
        public IActionResult ShortenUrl(PostUrlVM postUrlVM)
        {
            if (!ModelState.IsValid)
            {
                return View("Index",postUrlVM);
            }

			var loggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var newUrl = new Url()
            {
                OriginalLink = postUrlVM.Url,
                ShortLink = GenerateShortUrl(6),
                NrOfClicks = 0,
                UserId = loggedInUserId,
                DateCreated = DateTime.UtcNow
            };

            _context.Urls.Add(newUrl);
            _context.SaveChanges();

            TempData["Message"] = $"Your url was shorted successfully to {newUrl.ShortLink}";
            return RedirectToAction("Index");
        }
        private string GenerateShortUrl(int length)
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

            return new string(
                Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)])
                .ToArray()
                );
        }


    }
}
