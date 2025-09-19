using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Running;
using DotNetDev.AspNetCore.Http.Benchmarks;

var config = ManualConfig.CreateMinimumViable();

config.ArtifactsPath = "benchmarks";

config.AddExporter(HtmlExporter.Default);

var _ = BenchmarkSwitcher.FromAssembly(typeof(FormFeatureBenchmarks).Assembly)
				 .RunAll(config, args);
