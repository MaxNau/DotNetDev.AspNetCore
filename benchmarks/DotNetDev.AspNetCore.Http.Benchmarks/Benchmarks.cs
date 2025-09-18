using System;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace DotNetDev.AspNetCore.Http.Benchmarks;

// For more information on the VS BenchmarkDotNet Diagnosers see https://learn.microsoft.com/visualstudio/profiling/profiling-with-benchmark-dotnet
[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class Benchmarks
{
	private readonly SHA256 sha256 = SHA256.Create();
	private byte[] data;

	[GlobalSetup]
	public void Setup()
	{
		data = new byte[10000];
		new Random(42).NextBytes(data);
	}

	[Benchmark]
	public byte[] Sha256()
	{
		byte[] temp = new byte[100000];
		var x = temp;
		return sha256.ComputeHash(data);
	}
}
