using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniSecretApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    private static readonly string[] AllowedExtensions =
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private const long MaxFileSize =
        10 * 1024 * 1024;

    public UploadsController(
        IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpPost("confessions")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> UploadConfessionImage(
        IFormFile file)
    {
        if (file == null ||
            file.Length == 0)
        {
            return BadRequest(new
            {
                message = "No image file was uploaded."
            });
        }

        if (file.Length > MaxFileSize)
        {
            return BadRequest(new
            {
                message =
                    "Image must be smaller than 10 MB."
            });
        }

        var extension =
            Path.GetExtension(
                file.FileName
            ).ToLowerInvariant();

        if (!AllowedExtensions.Contains(
                extension))
        {
            return BadRequest(new
            {
                message =
                    "Only JPG, JPEG, PNG and WEBP images are allowed."
            });
        }

        /**
         * Generate a random filename.
         *
         * Never trust the original filename.
         */
        var fileName =
            $"{Guid.NewGuid():N}{extension}";

        var uploadsFolder =
            Path.Combine(
                _environment.WebRootPath ??
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot"
                ),
                "uploads",
                "confessions"
            );

        Directory.CreateDirectory(
            uploadsFolder
        );

        var filePath =
            Path.Combine(
                uploadsFolder,
                fileName
            );

        await using (
            var stream =
                new FileStream(
                    filePath,
                    FileMode.CreateNew
                ))
        {
            await file.CopyToAsync(
                stream
            );
        }

        var imageUrl =
            $"{Request.Scheme}://{Request.Host}/uploads/confessions/{fileName}";
    

        return Ok(new
        {
            imageUrl
        });
    }
}