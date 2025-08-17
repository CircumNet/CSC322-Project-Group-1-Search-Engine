//
// FILE: Controllers/SearchController.cs
//
// This file handles the web requests for the search functionality.
// It serves the search pages and processes the search queries.
//
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using SearchEngine.Models;
using SearchEngine.Services;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SearchEngine.Controllers
{
    public class SearchController : Controller
    {
        private readonly SearchEngineService _searchService;
        private readonly IHostEnvironment _hostEnvironment;

        public SearchController(SearchEngineService searchService, IHostEnvironment hostEnvironment)
        {
            _searchService = searchService;
            _hostEnvironment = hostEnvironment;
        }

        // GET: /Search/Index
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Search/Results?query=your+search+terms
        public IActionResult Results(string query)
        {
            IEnumerable<SearchResultItem> results = _searchService.Search(query);
            ViewData["Query"] = query;

            // If no direct results are found, get ranked suggestions
            if (!results.Any())
            {
                IEnumerable<SearchResultItem> suggestions = _searchService.GetRankedSuggestions(query);
                ViewData["Suggestions"] = suggestions.ToList();
            }

            return View(results.ToList());
        }

        // POST: /Search/Upload
        [HttpPost]
        public async Task<IActionResult> Upload(List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
            {
                return RedirectToAction("Index"); // Or return an error view
            }

            string uploadsPath = Path.Combine(_hostEnvironment.ContentRootPath, "uploads");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            foreach (IFormFile file in files)
            {
                if (file.Length > 0)
                {
                    string filePath = Path.Combine(uploadsPath, Path.GetFileName(file.FileName));

                    using (FileStream stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    _searchService.IndexUploadedFile(filePath);
                }
            }

            return RedirectToAction("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
