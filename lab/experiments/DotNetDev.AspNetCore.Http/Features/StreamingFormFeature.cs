using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace DotNetDev.AspNetCore.Http.Features;

public class StreamingFormFeature : IFormFeature
{
	private readonly HttpRequest? _request;
	private readonly Endpoint? _endpoint;
	private readonly FormOptions _options;
	private Task<IFormCollection>? _parsedFormTask;
	private readonly IFormCollection? _form;
	private readonly MediaTypeHeaderValue? _formContentType; // null iff _form is null

	public StreamingFormFeature(IFormCollection form)
	{
		Form = form;
		_formContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
		_options = new FormOptions();
	}

	public StreamingFormFeature(HttpRequest request)
		: this(request, new FormOptions())
	{
	}

	public StreamingFormFeature(HttpRequest request, FormOptions options)
		 : this(request, options, null)
	{
	}

	internal StreamingFormFeature(HttpRequest request, FormOptions options, Endpoint? endpoint)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(options);

		_request = request;
		_options = options;
		_endpoint = endpoint;
	}

	private MediaTypeHeaderValue? ContentType
	{
		get
		{
			MediaTypeHeaderValue? mt = null;

			if (_request is not null)
			{
				_ = MediaTypeHeaderValue.TryParse(_request.ContentType, out mt);
			}

			if (_form is not null && mt is null)
			{
				mt = _formContentType;
			}

			return mt;
		}
	}

	public bool HasFormContentType
	{
		get
		{
			// Set directly
			if (Form != null)
			{
				return true;
			}

			if (_request is null)
			{
				return false;
			}

			var contentType = ContentType;
			return HasApplicationFormContentType(contentType) || HasMultipartFormContentType(contentType);
		}
	}

	public IFormCollection? Form { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

	public IFormCollection ReadForm()
	{
		throw new NotImplementedException();
	}

	public Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken)
	{
		HandleUncheckedAntiforgeryValidationFeature();
		// Avoid state machine and task allocation for repeated reads
		if (_parsedFormTask == null)
		{
			if (Form != null)
			{
				_parsedFormTask = Task.FromResult(Form);
			}
			else
			{
				_parsedFormTask = InnerReadFormAsync(cancellationToken);
			}
		}
		return _parsedFormTask;
	}

	private async Task<IFormCollection> InnerReadFormAsync(CancellationToken cancellationToken)
	{
		if (_request is null)
		{
			throw new InvalidOperationException("Cannot read form from this request. Request is 'null'.");
		}

		HandleUncheckedAntiforgeryValidationFeature();
		//_options = _endpoint is null ? _options : GetFormOptionsFromMetadata(_options, _endpoint);

		if (!HasFormContentType)
		{
			throw new InvalidOperationException("Incorrect Content-Type: " + _request.ContentType);
		}

		cancellationToken.ThrowIfCancellationRequested();

		if (_request.ContentLength == 0)
		{
			return FormCollection.Empty;
		}

		FormCollection? formFields = null;
		FormFileCollection? files = null;

		// Some of these code paths use StreamReader which does not support cancellation tokens.
		using (cancellationToken.Register((state) => ((HttpContext)state!).Abort(), _request.HttpContext))
		{
			var contentType = ContentType;
			// Check the content-type
			if (HasApplicationFormContentType(contentType))
			{
				var encoding = FilterEncoding(contentType.Encoding);
				var formReader = new FormPipeReader(_request.BodyReader, encoding)
				{
					ValueCountLimit = _options.ValueCountLimit,
					KeyLengthLimit = _options.KeyLengthLimit,
					ValueLengthLimit = _options.ValueLengthLimit,
				};
				formFields = new FormCollection(await formReader.ReadFormAsync(cancellationToken));
			}
			else if (HasMultipartFormContentType(contentType))
			{
				var formAccumulator = new KeyValueAccumulator();
				var sectionCount = 0;

				var boundary = GetBoundary(contentType, _options.MultipartBoundaryLengthLimit);
				var multipartReader = new MultipartReader(boundary, _request.Body)
				{
					HeadersCountLimit = _options.MultipartHeadersCountLimit,
					HeadersLengthLimit = _options.MultipartHeadersLengthLimit,
					BodyLengthLimit = _options.MultipartBodyLengthLimit,
				};
				var section = await multipartReader.ReadNextSectionAsync(cancellationToken);
				while (section != null)
				{
					sectionCount++;
					if (sectionCount > _options.ValueCountLimit)
					{
						throw new InvalidDataException($"Form value count limit {_options.ValueCountLimit} exceeded.");
					}
					// Parse the content disposition here and pass it further to avoid reparsings
					if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition))
					{
						throw new InvalidDataException("Form section has invalid Content-Disposition value: " + section.ContentDisposition);
					}

					if (contentDisposition.IsFileDisposition())
					{
						var fileSection = new FileMultipartSection(section, contentDisposition);

						// Find the end
						await section.Body.DrainAsync(cancellationToken);

						var name = fileSection.Name;
						var fileName = fileSection.FileName;

						FormFile file;
						if (section.BaseStreamOffset.HasValue)
						{
							// Relative reference to buffered request body
							file = new FormFile(_request.Body, section.BaseStreamOffset.GetValueOrDefault(), section.Body.Length, name, fileName);
						}
						else
						{
							// Individually buffered file body
							file = new FormFile(section.Body, 0, section.Body.Length, name, fileName);
						}
						file.Headers = new HeaderDictionary(section.Headers);

						if (files == null)
						{
							files = new FormFileCollection();
						}
						files.Add(file);
					}
					else if (contentDisposition.IsFormDisposition())
					{
						var formDataSection = new FormMultipartSection(section, contentDisposition);

						// Content-Disposition: form-data; name="key"
						//
						// value

						// Do not limit the key name length here because the multipart headers length limit is already in effect.
						var key = formDataSection.Name;
						var value = await formDataSection.GetValueAsync(cancellationToken);

						formAccumulator.Append(key, value);
					}
					else
					{
						// Ignore form sections with invalid content disposition
					}

					section = await multipartReader.ReadNextSectionAsync(cancellationToken);
				}

				if (formAccumulator.HasValues)
				{
					formFields = new FormCollection(formAccumulator.GetResults(), files);
				}
			}
		}

		// Rewind so later readers don't have to.
		if (_request.Body.CanSeek)
		{
			_request.Body.Seek(0, SeekOrigin.Begin);
		}

		if (formFields != null)
		{
			Form = formFields;
		}
		else if (files != null)
		{
			Form = new FormCollection(null, files);
		}
		else
		{
			Form = FormCollection.Empty;
		}

		return Form;
	}

	private static Encoding FilterEncoding(Encoding? encoding)
	{
		// UTF-7 is insecure and should not be honored. UTF-8 will succeed for most cases.
		// https://learn.microsoft.com/dotnet/core/compatibility/syslib-warnings/syslib0001
		if (encoding == null || encoding.CodePage == 65000)
		{
			return Encoding.UTF8;
		}
		return encoding;
	}

	private static bool HasApplicationFormContentType([NotNullWhen(true)] MediaTypeHeaderValue? contentType)
	{
		// Content-Type: application/x-www-form-urlencoded; charset=utf-8
		return contentType != null && contentType.MediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
	}

	private static bool HasMultipartFormContentType([NotNullWhen(true)] MediaTypeHeaderValue? contentType)
	{
		// Content-Type: multipart/form-data; boundary=----WebKitFormBoundarymx2fSWqWSd0OxQqq
		return contentType != null && contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase);
	}

	private bool ResolveHasInvalidAntiforgeryValidationFeature()
	{
		if (_request is null)
		{
			return false;
		}
		var hasInvokedMiddleware = _request.HttpContext.Items.ContainsKey("__AntiforgeryMiddlewareWithEndpointInvoked");
		var hasInvalidToken = _request.HttpContext.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false };
		return hasInvokedMiddleware && hasInvalidToken;
	}
	internal bool HasInvalidAntiforgeryValidationFeature => ResolveHasInvalidAntiforgeryValidationFeature();

	private void HandleUncheckedAntiforgeryValidationFeature()
	{
		if (HasInvalidAntiforgeryValidationFeature)
		{
			throw new InvalidOperationException("This form is being accessed with an invalid anti-forgery token. Validate the `IAntiforgeryValidationFeature` on the request before reading from the form.");
		}
	}

	// Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
	// The spec says 70 characters is a reasonable limit.
	private static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
	{
		var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary);
		if (StringSegment.IsNullOrEmpty(boundary))
		{
			throw new InvalidDataException("Missing content-type boundary.");
		}
		if (boundary.Length > lengthLimit)
		{
			throw new InvalidDataException($"Multipart boundary length limit {lengthLimit} exceeded.");
		}
		return boundary.ToString();
	}

	//private static FormOptions GetFormOptionsFromMetadata(FormOptions baseFormOptions, Endpoint endpoint)
	//{
	//	var formOptionsMetadatas = endpoint.Metadata
	//		.GetOrderedMetadata<IFormOptionsMetadata>();
	//	var metadataCount = formOptionsMetadatas.Count;
	//	if (metadataCount == 0)
	//	{
	//		return baseFormOptions;
	//	}
	//	var finalFormOptionsMetadata = new MutableFormOptionsMetadata(formOptionsMetadatas[metadataCount - 1]);
	//	for (int i = metadataCount - 2; i >= 0; i--)
	//	{
	//		formOptionsMetadatas[i].MergeWith(ref finalFormOptionsMetadata);
	//	}
	//	return finalFormOptionsMetadata.ResolveFormOptions(baseFormOptions);
	//}
}
