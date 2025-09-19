using Microsoft.AspNetCore.Mvc;

namespace DotNetDev.Lab.Apps.FormUploadBuffering.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FileUploadController : ControllerBase
{
	[HttpPost("upload")]
	public async Task<IActionResult> UploadFileAsync(IFormFile file)
	{
		await Task.CompletedTask;

		return Ok();
	}
}
