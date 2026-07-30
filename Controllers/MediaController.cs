using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly FixitDbContext _db;

    public MediaController(FixitDbContext db) => _db = db;

    [HttpGet("{id:guid}")]
    [ResponseCache(Duration = 86400)]
    public async Task<IActionResult> Get(Guid id)
    {
        var media = await _db.MediaFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (media == null)
            return NotFound();

        Response.Headers["Cache-Control"] = "public, max-age=86400";

        return File(media.Data, media.ContentType);
    }
}
