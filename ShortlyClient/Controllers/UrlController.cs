using Microsoft.AspNetCore.Mvc;
using ShortlyClient.Helpers.Roles;
using ShortlyClient.Data.ViewModels;
using ShortlyData.Models;
using AutoMapper;
using Shortly.Data.Services;
using System.Security.Claims;

namespace Shortly.Client.Controllers
{
    public class UrlController : Controller
    {
        private IUrlsService _urlsService;
        private readonly IMapper _mapper;
        public UrlController(IUrlsService urlsService, IMapper mapper)
        {
            _urlsService = urlsService;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index()
        {
            var loggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole(Role.Admin);

            var allUrls = await _urlsService.GetUrlAsync(loggedInUserId, isAdmin);
            var mappedAllUrls = _mapper.Map<List<Url>, List<GetUrlVM>>(allUrls);

            return View(mappedAllUrls);
        }

        public async Task<IActionResult> Create()
        {
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Remove(int id)
        {
            await _urlsService.DeleteAsync(id);
            return RedirectToAction("Index");
        }
    }
}