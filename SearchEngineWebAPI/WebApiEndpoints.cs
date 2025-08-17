//
// FILE: WebApiEndpoints.cs
//
// This file shows how you could expose the search functionality as a pure API.
//
using Microsoft.AspNetCore.Mvc;
using SearchEngine.Models;
using SearchEngine.Services;

namespace SearchEngine.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebApiEndpoints : ControllerBase
    {
        private readonly SearchEngineService _searchService;

        public WebApiEndpoints(SearchEngineService searchService)
        {
            _searchService = searchService;
        }

        // POST api/search
        [HttpPost]
        public IActionResult Post([FromBody] string query)
        {
            IEnumerable<SearchResultItem> results = _searchService.Search(query);
            return Ok(results);
        }
    }
}
