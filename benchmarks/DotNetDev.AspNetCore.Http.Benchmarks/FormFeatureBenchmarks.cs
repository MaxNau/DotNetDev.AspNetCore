using System;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using DotNetDev.TestBench.Shared.Factories.Apps;
using DotNetDev.TestBench.Shared.Utils;
using Microsoft.AspNetCore.TestHost;

namespace DotNetDev.AspNetCore.Http.Benchmarks;

[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[ShortRunJob]
public class FormFeatureBenchmarks
{
	private const string Boundary = "----test";

	private TestServer _server = default!;
	private Stream _multipartStream = default!;

	private string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

	[Params("10MB.bin", "25MB.bin", "50MB.bin")]
	public string FileName;

	[GlobalSetup]
	public void Setup()
	{
		var app = new FormUploadBufferingWebApplicationFactory();
		_server = app.Server;
	}

	[IterationSetup]
	public void IterationSetup()
	{
		_multipartStream = MultipartHelper.CreateMultipartStream(FilePath, Boundary);
	}

	[Benchmark(Baseline = true)]
	public async Task Microsoft_AspNetCore_Http_Features_FormFeature_Using_TestServer()
	{
		var _ = await _server.SendAsync(configureContext =>
		{
			var request = configureContext.Request;

			request.Method = "POST";
			request.Path = "/api/fileupload/upload";
			request.ContentType = $"multipart/form-data; boundary={Boundary}";

			request.Body = _multipartStream;
		});
	}

	[IterationCleanup]
	public void IterationCleanup()
	{
		_multipartStream.Dispose();
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_server.Dispose();
	}
}
