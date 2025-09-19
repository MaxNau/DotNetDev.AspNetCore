using DotNetDev.Lab.Apps.FormUploadBuffering;
using DotNetDev.Lab.Apps.FormUploadBuffering.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetDev.TestBench.Shared.Factories.Apps;

public class FormUploadBufferingWebApplicationFactory : WebApplicationFactory<FormUploadBufferingProgram>
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureServices(services =>
		{
			services.Configure<ExperimentalFeatureSettings>(options =>
			{
				options.EnableExperimentalFormFeature = true;
			});
		});

		base.ConfigureWebHost(builder);
	}
}
