using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Search;
using Microsoft.AspNetCore.Mvc;

namespace CitadelIQ.Api.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController(ISearchService searchService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<SearchResultDto>>> Search(
        [FromBody] SearchRequestDto request,
        CancellationToken cancellationToken)
    {
        var results = await searchService.SearchAsync(request, cancellationToken);
        return Ok(results);
    }
}
