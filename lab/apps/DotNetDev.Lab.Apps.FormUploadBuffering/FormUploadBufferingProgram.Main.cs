using Microsoft.AspNetCore.Http.Features;

const long FileUploadSizeLimit = 5_368_709_120; // 5 GB

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
	options.Limits.MaxRequestBodySize = FileUploadSizeLimit;
});

builder.Services.Configure<FormOptions>(options =>
{
	options.MultipartBodyLengthLimit = FileUploadSizeLimit;
});

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

await app.RunAsync();
