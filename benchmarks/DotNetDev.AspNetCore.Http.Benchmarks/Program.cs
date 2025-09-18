using BenchmarkDotNet.Running;

namespace DotNetDev.AspNetCore.Http.Benchmarks;

internal class Program
{
	static void Main(string[] args)
	{

		var _ = BenchmarkRunner.Run(typeof(Program).Assembly);
	}
}
