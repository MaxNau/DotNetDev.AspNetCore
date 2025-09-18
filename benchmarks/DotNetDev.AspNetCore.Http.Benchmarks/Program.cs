using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;

namespace DotNetDev.AspNetCore.Http.Benchmarks;

internal class Program
{
	static void Main(string[] args)
	{
		var config = ManualConfig.CreateEmpty();

		config.AddExporter(JsonExporter.Default);
		config.AddExporter(CsvExporter.Default);
		config.AddExporter(MarkdownExporter.Default);

		var _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
						 .RunAll(config, args);
	}
}
