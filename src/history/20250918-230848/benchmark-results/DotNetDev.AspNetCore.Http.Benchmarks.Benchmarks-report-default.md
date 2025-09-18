
BenchmarkDotNet v0.15.2, Linux Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.100-rc.1.25451.107
  [Host]     : .NET 10.0.0 (10.0.25.45207), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.0 (10.0.25.45207), X64 RyuJIT AVX2


 Method | Mean     | Error     | StdDev    | Allocated |
------- |---------:|----------:|----------:|----------:|
 Sha256 | 6.838 μs | 0.0253 μs | 0.0225 μs |     112 B |
