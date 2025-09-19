namespace DotNetDev.TestBench.Shared.Utils;

using System.IO.Pipelines;
using System.Text;

public static class MultipartHelper
{
	/// <summary>
	/// Creates a multipart/form-data stream for a single file.
	/// </summary>
	public static Stream CreateMultipartStream(
		string filePath,
		string boundary,
		string formFieldName = "file",
		string contentType = "application/octet-stream")
	{
		var pipe = new Pipe();

		_ = Task.Run(async () =>
		{
			try
			{
				var writer = pipe.Writer;
				var fileName = Path.GetFileName(filePath);

				// Write header
				await WriteHeaderAsync(writer, boundary, formFieldName, fileName, contentType);

				// Write file content
				await WriteFileContentAsync(writer, filePath);

				// Write footer
				await WriteFooterAsync(writer, boundary);

				await writer.CompleteAsync();
			}
			catch (Exception ex)
			{
				await pipe.Writer.CompleteAsync(ex);
			}
		});

		return pipe.Reader.AsStream();
	}

	private static async Task WriteHeaderAsync(
		PipeWriter writer,
		string boundary,
		string fieldName,
		string fileName,
		string contentType)
	{
		var header = $"--{boundary}\r\n" +
					 $"Content-Disposition: form-data; name=\"{fieldName}\"; filename=\"{fileName}\"\r\n" +
					 $"Content-Type: {contentType}\r\n\r\n";
		await writer.WriteAsync(Encoding.UTF8.GetBytes(header));
	}

	private static async Task WriteFileContentAsync(PipeWriter writer, string filePath)
	{
		await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
		var buffer = new byte[8192];
		int read;
		while ((read = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
		{
			var memory = writer.GetMemory(read);
			buffer.AsSpan(0, read).CopyTo(memory.Span);
			writer.Advance(read);
			var result = await writer.FlushAsync();
			if (result.IsCompleted) break;
		}
	}

	private static async Task WriteFooterAsync(PipeWriter writer, string boundary)
	{
		var footer = $"\r\n--{boundary}--\r\n";
		await writer.WriteAsync(Encoding.UTF8.GetBytes(footer));
	}
}
