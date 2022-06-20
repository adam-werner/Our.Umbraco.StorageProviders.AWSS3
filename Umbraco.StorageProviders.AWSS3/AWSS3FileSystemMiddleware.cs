using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Umbraco.Cms.Core.Hosting;
using Umbraco.StorageProviders.AWSS3.IO;

namespace Umbraco.StorageProviders.AWSS3
{
    public class AWSS3FileSystemMiddleware : IMiddleware
    {
        private readonly string _name;
        private readonly IAWSS3FileSystemProvider _fileSystemProvider;
        private string _rootPath;
        private string _containerRootPath;
        private string _bucketName;
        private readonly TimeSpan? _maxAge = TimeSpan.FromDays(7);

        /// <summary>
        /// Creates a new instance of <see cref="AWSS3FileSystemMiddleware" />.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="fileSystemProvider">The file system provider.</param>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// 
        public AWSS3FileSystemMiddleware(IOptionsMonitor<AWSS3FileSystemOptions> options, IAWSS3FileSystemProvider fileSystemProvider, IHostingEnvironment hostingEnvironment)
            : this(AWSS3FileSystemOptions.MediaFileSystemName, options, fileSystemProvider, hostingEnvironment)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AWSS3FileSystemMiddleware" /> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <param name="fileSystemProvider">The file system provider.</param>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <exception cref="System.ArgumentNullException">options
        /// or
        /// hostingEnvironment
        /// or
        /// name
        /// or
        /// fileSystemProvider</exception>
        protected AWSS3FileSystemMiddleware(string name, IOptionsMonitor<AWSS3FileSystemOptions> options, IAWSS3FileSystemProvider fileSystemProvider, IHostingEnvironment hostingEnvironment)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (hostingEnvironment == null) throw new ArgumentNullException(nameof(hostingEnvironment));

            _name = name ?? throw new ArgumentNullException(nameof(name));
            _fileSystemProvider = fileSystemProvider ?? throw new ArgumentNullException(nameof(fileSystemProvider));

            var fileSystemOptions = options.Get(name);
            _rootPath = hostingEnvironment.ToAbsolute(fileSystemOptions.VirtualPath);
            _containerRootPath = fileSystemOptions.BucketPrefix ?? _rootPath;
            _bucketName = fileSystemOptions.BucketName;

            options.OnChange((o, n) => OptionsOnChange(o, n, hostingEnvironment));
        }

        /// <inheritdoc />
        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (next == null) throw new ArgumentNullException(nameof(next));

            return HandleRequestAsync(context, next);
        }

        private async Task HandleRequestAsync(HttpContext context, RequestDelegate next)
        {
            var request = context.Request;
            var response = context.Response;

            if (!context.Request.Path.StartsWithSegments(_rootPath, StringComparison.InvariantCultureIgnoreCase))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            string containerPath = $"{_containerRootPath.TrimEnd('/')}/{(request.Path.Value.Remove(0, _rootPath.Length)).TrimStart('/')}";
            var s3Client = _fileSystemProvider.GetFileSystem(_name).GetS3Client(containerPath);

            var s3RequestConditions = GetAccessCondition(context.Request, containerPath, _bucketName);
            
            GetObjectMetadataResponse objectProperties = null;
            var ignoreRange = false;

            try
            {
                objectProperties = (await s3Client.GetObjectMetadataAsync(s3RequestConditions));
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // the bucket or file does not exist, let other middleware handle it
                await next(context).ConfigureAwait(false);
                return;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // If-Range or If-Unmodified-Since is not met
                // if the resource has been modified, we need to send the whole file back with a 200 OK
                // a Content-Range header is needed with the new length
                ignoreRange = true;
                objectProperties = await s3Client.GetObjectMetadataAsync(s3RequestConditions).ConfigureAwait(false);
                response.Headers.Append("Content-Range", $"bytes */{objectProperties.Headers.ContentLength}");
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotModified)
            {
                // If-None-Match or If-Modified-Since is not met
                // we need to pass the status code back to the client
                // so it knows it can reuse the cached data
                response.StatusCode = (int)HttpStatusCode.NotModified;
                return;
            }
            // for some reason we get an internal exception type with the message
            // and not a request failed with status NotModified :(
            catch (Exception ex) when (ex.Message == "The condition specified using HTTP conditional header(s) is not met.")
            {
                if (s3RequestConditions != null
                    && (s3RequestConditions.EtagToMatch != "" || s3RequestConditions.UnmodifiedSinceDateUtc != DateTime.MinValue))
                {
                    // If-Range or If-Unmodified-Since is not met
                    // if the resource has been modified, we need to send the whole file back with a 200 OK
                    // a Content-Range header is needed with the new length
                    ignoreRange = true;
                    objectProperties = await s3Client.GetObjectMetadataAsync(s3RequestConditions).ConfigureAwait(false);
                    response.Headers.Append("Content-Range", $"bytes */{objectProperties.ContentLength}");
                }
                else
                {
                    // If-None-Match or If-Modified-Since is not met
                    // we need to pass the status code back to the client
                    // so it knows it can reuse the cached data
                    response.StatusCode = (int)HttpStatusCode.NotModified;
                    return;
                }
            }
            catch (TaskCanceledException)
            {
                // client cancelled the request before it could finish, just ignore
                return;
            }

            var responseHeaders = response.GetTypedHeaders();

            responseHeaders.CacheControl =
                new CacheControlHeaderValue
                {
                    Public = true,
                    MustRevalidate = true,
                    MaxAge = _maxAge,
                };

            responseHeaders.LastModified = objectProperties.LastModified;
            responseHeaders.ETag = new EntityTagHeaderValue($"{objectProperties.ETag}");
            responseHeaders.Append(HeaderNames.Vary, "Accept-Encoding");

            var requestHeaders = request.GetTypedHeaders();

            var rangeHeader = requestHeaders.Range;

            if (!ignoreRange && rangeHeader != null)
            {
                if (!ValidateRanges(rangeHeader.Ranges, objectProperties.ContentLength))
                {
                    // no ranges could be parsed
                    response.Clear();
                    response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                    responseHeaders.ContentRange = new ContentRangeHeaderValue(objectProperties.ContentLength);
                    return;
                }

                if (rangeHeader.Ranges.Count == 1)
                {
                    var range = rangeHeader.Ranges.First();
                    var contentRange = GetRangeHeader(objectProperties, range);

                    response.StatusCode = (int)HttpStatusCode.PartialContent;
                    response.ContentType = objectProperties.Headers.ContentType;
                    responseHeaders.ContentRange = contentRange;

                    await DownloadRangeToStreamAsync(s3Client, objectProperties, response.Body, contentRange, context.RequestAborted,
                        _bucketName, containerPath).ConfigureAwait(false);
                    return;
                }

                if (rangeHeader.Ranges.Count > 1)
                {
                    // handle multipart ranges
                    var boundary = Guid.NewGuid().ToString();
                    response.StatusCode = (int)HttpStatusCode.PartialContent;
                    response.ContentType = $"multipart/byteranges; boundary={boundary}";

                    foreach (var range in rangeHeader.Ranges)
                    {
                        var contentRange = GetRangeHeader(objectProperties, range);

                        await response.WriteAsync($"--{boundary}").ConfigureAwait(false);
                        await response.WriteAsync("\n").ConfigureAwait(false);
                        await response.WriteAsync($"{HeaderNames.ContentType}: {objectProperties.Headers.ContentType}").ConfigureAwait(false);
                        await response.WriteAsync("\n").ConfigureAwait(false);
                        await response.WriteAsync($"{HeaderNames.ContentRange}: {contentRange}").ConfigureAwait(false);
                        await response.WriteAsync("\n").ConfigureAwait(false);
                        await response.WriteAsync("\n").ConfigureAwait(false);

                        await DownloadRangeToStreamAsync(s3Client, objectProperties, response.Body, contentRange, context.RequestAborted,
                            _bucketName, containerPath).ConfigureAwait(false);
                        await response.WriteAsync("\n").ConfigureAwait(false);
                    }

                    await response.WriteAsync($"--{boundary}--").ConfigureAwait(false);
                    await response.WriteAsync("\n").ConfigureAwait(false);
                    return;
                }
            }
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = objectProperties.Headers.ContentType;
            responseHeaders.ContentLength = objectProperties.ContentLength;
            responseHeaders.Append(HeaderNames.AcceptRanges, "bytes");

            await response.StartAsync().ConfigureAwait(false);
            await DownloadRangeToStreamAsync(s3Client, response.Body, 0L, objectProperties.ContentLength, context.RequestAborted, _bucketName, containerPath).ConfigureAwait(false);
        }

        private static GetObjectMetadataRequest GetAccessCondition(HttpRequest request, String key, String bucketName)
        {
            var range = request.Headers["Range"];

            var getObjectMetadataRequest = new GetObjectMetadataRequest
            {
                Key = key,
                BucketName = bucketName
            };
            if (string.IsNullOrEmpty(range))
            {
                // etag
                var ifNoneMatch = request.Headers["If-None-Match"];
                if (!string.IsNullOrEmpty(ifNoneMatch))
                {
                    getObjectMetadataRequest.EtagToNotMatch = ifNoneMatch;
                    return getObjectMetadataRequest;
                }

                var ifModifiedSince = request.Headers["If-Modified-Since"];
                if (!string.IsNullOrEmpty(ifModifiedSince))
                {
                    getObjectMetadataRequest.ModifiedSinceDateUtc =
                        DateTime.Parse(ifModifiedSince, CultureInfo.InvariantCulture);
                    return getObjectMetadataRequest;
                }
            }
            else
            {
                // handle If-Range header, it can be either an etag or a date
                // see https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/If-Range and https://tools.ietf.org/html/rfc7233#section-3.2
                var ifRange = request.Headers["If-Range"];
                if (!string.IsNullOrEmpty(ifRange))
                {
                    var conditions = new GetObjectMetadataRequest();

                    if (DateTime.TryParse(ifRange, out var date))
                    {
                        conditions.UnmodifiedSinceDateUtc = date;
                    }
                    else
                    {
                        conditions.EtagToMatch = ifRange;
                    }
                }

                var ifUnmodifiedSince = request.Headers["If-Unmodified-Since"];
                if (!string.IsNullOrEmpty(ifUnmodifiedSince))
                {

                    getObjectMetadataRequest.UnmodifiedSinceDateUtc =
                        DateTime.Parse(ifUnmodifiedSince, CultureInfo.InvariantCulture);
                    return getObjectMetadataRequest;
                }
            }

            return getObjectMetadataRequest;
        }

        private static bool ValidateRanges(ICollection<RangeItemHeaderValue> ranges, long length)
        {
            if (ranges.Count == 0)
                return false;

            foreach (var range in ranges)
            {
                if (range.From > range.To)
                    return false;
                if (range.To >= length)
                    return false;
            }

            return true;
        }

        private static ContentRangeHeaderValue GetRangeHeader(GetObjectMetadataResponse properties, RangeItemHeaderValue range)
        {
            var length = properties.ContentLength - 1;

            long from;
            long to;
            if (range.To.HasValue)
            {
                if (range.From.HasValue)
                {
                    to = Math.Min(range.To.Value, length);
                    from = range.From.Value;
                }
                else
                {
                    to = length;
                    from = Math.Max(properties.ContentLength - range.To.Value, 0L);
                }
            }
            else if (range.From.HasValue)
            {
                to = length;
                from = range.From.Value;
            }
            else
            {
                to = length;
                from = 0L;
            }

            return new ContentRangeHeaderValue(from, to, properties.ContentLength);
        }

        private static async Task DownloadRangeToStreamAsync(IAmazonS3 s3Client, GetObjectMetadataResponse properties,
            Stream outputStream, ContentRangeHeaderValue contentRange, CancellationToken cancellationToken, String bucketName, String key)
        {
            var offset = contentRange.From.GetValueOrDefault(0L);
            var length = properties.ContentLength;

            if (contentRange.To.HasValue && contentRange.From.HasValue)
            {
                length = contentRange.To.Value - contentRange.From.Value + 1;
            }
            else if (contentRange.To.HasValue)
            {
                length = contentRange.To.Value + 1;
            }
            else if (contentRange.From.HasValue)
            {
                length = properties.ContentLength - contentRange.From.Value + 1;
            }

            await DownloadRangeToStreamAsync(s3Client, outputStream, offset, length, cancellationToken, bucketName, key).ConfigureAwait(false);
        }

        private static async Task DownloadRangeToStreamAsync(IAmazonS3 s3Client, Stream outputStream,
            long offset, long length, CancellationToken cancellationToken, String bucketName, String key)
        {
            try
            {
                if (length == 0) return;

                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    ByteRange =  new ByteRange(offset, length)
                };

                var response = await s3Client.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);
                await response.ResponseStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // client cancelled the request before it could finish, just ignore
            }
        }

        private void OptionsOnChange(AWSS3FileSystemOptions options, string name, IHostingEnvironment hostingEnvironment)
        {
            if (name != _name) return;

            _rootPath = hostingEnvironment.ToAbsolute(options.VirtualPath);
            _containerRootPath = options.VirtualPath ?? _rootPath;
        }
    }
}
