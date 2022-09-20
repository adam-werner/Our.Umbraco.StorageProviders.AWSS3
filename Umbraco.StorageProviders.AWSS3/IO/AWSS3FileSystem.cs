using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.StaticFiles;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;
using Our.Umbraco.StorageProviders.AWSS3.Services;
using Microsoft.Extensions.Logging;

namespace Our.Umbraco.StorageProviders.AWSS3.IO
{
    public class AWSS3FileSystem : IAWSS3FileSystem
    {
        private readonly IContentTypeProvider _contentTypeProvider;
        private readonly IAmazonS3 _S3Client;
        private readonly string _rootUrl;
        private readonly string _bucketPrefix;
        private readonly string _bucketName;
        private readonly string _rootPath;
        protected readonly ILogger<AWSS3FileSystem> _logger;
        private readonly IMimeTypeResolver _mimeTypeResolver;
        private readonly S3CannedACL _cannedACL;
        private readonly ServerSideEncryptionMethod _serverSideEncryptionMethod;

        protected const string Delimiter = "/";
        protected const int BatchSize = 1000;

        public bool CanAddPhysical => throw new NotImplementedException();

        /// <summary>
        ///     Creates a new instance of <see cref="AWSS3FileSystem" />.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="hostingEnvironment"></param>
        /// <param name="contentTypeProvider"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public AWSS3FileSystem(AWSS3FileSystemOptions options, IHostingEnvironment hostingEnvironment,
            IContentTypeProvider contentTypeProvider, ILogger<AWSS3FileSystem> logger, IMimeTypeResolver mimeTypeResolver, IAmazonS3 s3Client)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (hostingEnvironment == null) throw new ArgumentNullException(nameof(hostingEnvironment));

            _logger = logger;
            _contentTypeProvider = contentTypeProvider ?? throw new ArgumentNullException(nameof(contentTypeProvider));
            _bucketName = options.BucketName ?? throw new ArgumentNullException(nameof(contentTypeProvider));

            _rootUrl = EnsureUrlSeparatorChar(hostingEnvironment.ToAbsolute(options.VirtualPath)).TrimEnd('/');
            _bucketPrefix = AWSS3FileSystemOptions.BucketPrefix ?? _rootUrl;
            _cannedACL = options.CannedACL;
            _serverSideEncryptionMethod = options.ServerSideEncryptionMethod;
            _rootPath = hostingEnvironment.ToAbsolute(options.VirtualPath);

            _mimeTypeResolver = mimeTypeResolver;

            _S3Client = s3Client;
        }


        /// <inheritdoc />
        public IAmazonS3 GetS3Client(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            return _S3Client;
        }


        private static string EnsureUrlSeparatorChar(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            return path.Replace("\\", "/", StringComparison.InvariantCultureIgnoreCase);
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = "/";

            path = ResolveBucketPath(path, true);
            var request = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Delimiter = Delimiter,
                Prefix = path
            };

            var response = ExecuteWithContinuation(request);
            return response
                .SelectMany(p => p.CommonPrefixes)
                .Select(p => RemovePrefix(p))
                .ToArray();
        }

        public void DeleteDirectory(string path)
        {
            DeleteDirectory(path, false);
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            //List Objects To Delete
            var listRequest = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Prefix = ResolveBucketPath(path, true)
            };

            var listResponse = ExecuteWithContinuation(listRequest);
            var keys = listResponse
                .SelectMany(p => p.S3Objects)
                .Select(p => new KeyVersion { Key = p.Key })
                .ToArray();

            //Batch Deletion Requests
            //foreach (var items in keys.)
            //{
            //    var deleteRequest = new DeleteObjectsRequest
            //    {
            //        BucketName = Config.BucketName,
            //        Objects = items.ToList()
            //    };
            //    Execute(client => client.DeleteObjectsAsync(deleteRequest)).Wait();
            //}
        }

        public bool DirectoryExists(string path)
        {
            var request = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Prefix = ResolveBucketPath(path, true),
                MaxKeys = 1
            };

            var response = Execute(client => client.ListObjectsAsync(request)).Result;
            return response.S3Objects.Count > 0;
        }

        public void AddFile(string path, Stream stream)
        {
            AddFile(path, stream, true);
        }

        public void AddFile(string path, Stream stream, bool overrideIfExists)
        {
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = ResolveBucketPath(path),
                    CannedACL = _cannedACL,
                    ContentType = _mimeTypeResolver.Resolve(path),
                    InputStream = memoryStream,
                    ServerSideEncryptionMethod = _serverSideEncryptionMethod
                };

                var response = Execute(client => client.PutObjectAsync(request)).Result;
                _logger.LogInformation(string.Format("Object {0} Created, Id:{1}, Hash:{2}", path, response.VersionId, response.ETag));
            }
        }

        public IEnumerable<string> GetFiles(string path)
        {
            return GetFiles(path, "*.*");
        }

        public IEnumerable<string> GetFiles(string path, string filter)
        {
            path = ResolveBucketPath(path, true);

            string filename = Path.GetFileNameWithoutExtension(filter);
            if (filename.EndsWith("*"))
                filename = filename.Remove(filename.Length - 1);

            string ext = Path.GetExtension(filter);
            if (ext.Contains("*"))
                ext = string.Empty;

            var request = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Delimiter = Delimiter,
                Prefix = path + filename
            };

            var response = ExecuteWithContinuation(request);
            return response
                .SelectMany(p => p.S3Objects)
                .Select(p => RemovePrefix(p.Key))
                .Where(p => !string.IsNullOrEmpty(p) && p.EndsWith(ext))
                .ToArray();
        }

        public Stream OpenFile(string path)
        {

            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };

            MemoryStream stream;
            using (var response = Execute(client => client.GetObjectAsync(request)).Result)
            {
                stream = new MemoryStream();
                response.ResponseStream.CopyTo(stream);
            }

            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);
            
            return stream;
        }

        public void DeleteFile(string path)
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };
            Execute(client => client.DeleteObjectAsync(request));
        }

        public bool FileExists(string path)
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };

            try
            {
                Execute(client => client.GetObjectMetadataAsync(request));
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        public string GetRelativePath(string fullPathOrUrl)
        {
            if (string.IsNullOrEmpty(fullPathOrUrl))
                return string.Empty;

            //Strip protocol if not in hostname
            if (!_bucketName.StartsWith("http"))
            {
                if (fullPathOrUrl.StartsWith("https://"))
                {
                    fullPathOrUrl = fullPathOrUrl.Substring("https://".Length);
                }
                if (fullPathOrUrl.StartsWith("http://"))
                {
                    fullPathOrUrl = fullPathOrUrl.Substring("http://".Length);
                }
            }

            //Strip Hostname
            //if (fullPathOrUrl.StartsWith(_bucketName, StringComparison.InvariantCultureIgnoreCase))
            //{
            //    fullPathOrUrl = fullPathOrUrl.Substring(Config.BucketHostName.Length);
            //    fullPathOrUrl = fullPathOrUrl.TrimStart(Delimiter.ToCharArray());
            //}

            //Strip Virtual Path
            if (fullPathOrUrl.StartsWith(_rootPath, StringComparison.InvariantCultureIgnoreCase))
            {
                fullPathOrUrl = fullPathOrUrl.Substring(_rootPath.Length);
                fullPathOrUrl = fullPathOrUrl.TrimStart(Delimiter.ToCharArray());
            }

            //Strip Bucket Prefix
            if (fullPathOrUrl.StartsWith(_bucketPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                fullPathOrUrl = fullPathOrUrl.Substring(_bucketPrefix.Length);
                fullPathOrUrl = fullPathOrUrl.TrimStart(Delimiter.ToCharArray());
            }

            return fullPathOrUrl;
        }

        public string GetFullPath(string path)
        {
            return path;
        }

        public string GetUrl(string path)
        {
            var hostName = "";

            return string.Concat(hostName, "/", ResolveBucketPath(path));
        }

        public DateTimeOffset GetLastModified(string path)
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };

            var response = Execute(client => client.GetObjectMetadataAsync(request)).Result;
            return new DateTimeOffset(response.LastModified);
        }

        public DateTimeOffset GetCreated(string path)
        {
            //It Is Not Possible To Get Object Created Date - Bucket Versioning Required
            //Return Last Modified Date Instead
            return GetLastModified(path);
        }

        public long GetSize(string path)
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };

            var response = Execute(client => client.GetObjectMetadataAsync(request)).Result;
            return response.ContentLength;
        }

        public void AddFile(string path, string physicalPath, bool overrideIfExists = true, bool copy = false)
        {
            throw new NotImplementedException();
        }
        
        protected virtual T Execute<T>(Func<IAmazonS3, T> request)
        {
            try
            {
                return request(_S3Client);
            }
            catch (AmazonS3Exception ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    throw new FileNotFoundException(ex.Message, ex);
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new UnauthorizedAccessException(ex.Message, ex);

                _logger.LogError(ex, "S3 Bucket Error {0} {1}", ex.ErrorCode, ex.Message);
                throw;
            }
        }

        protected virtual IEnumerable<ListObjectsResponse> ExecuteWithContinuation(ListObjectsRequest request)
        {

            var response = Execute(client => client.ListObjectsAsync(request)).Result;
            yield return response;

            while (response.IsTruncated)
            {
                request.Marker = response.NextMarker;
                response = Execute(client => client.ListObjectsAsync(request)).Result;
                yield return response;
            }
        }

        public string ResolveBucketPath(string path, bool isDir = false)
        {
            if (string.IsNullOrEmpty(path))
                return _bucketPrefix;
            
            // Equalise delimiters
            path = path.Replace("/", Delimiter).Replace("\\", Delimiter);

            //Strip Root Path
            if (path.StartsWith(_rootPath, StringComparison.InvariantCultureIgnoreCase))
            {
                path = path.Substring(_rootPath.Length);
                path = path.TrimStart(Delimiter.ToCharArray());
            }

            if (path.StartsWith(Delimiter))
                path = path.Substring(1);

            //Remove Key Prefix If Duplicate
            if (path.StartsWith(_bucketPrefix, StringComparison.InvariantCultureIgnoreCase))
                path = path.Substring(_bucketPrefix.Length);

            if (isDir && !path.EndsWith(Delimiter))
                path = string.Concat(path, Delimiter);

            if (path.StartsWith(Delimiter))
                path = path.Substring(1);

            return string.Concat(_bucketPrefix, "/", path);
        }

        protected virtual string RemovePrefix(string key)
        {
            if (!string.IsNullOrEmpty(_bucketPrefix) && key.StartsWith(_bucketPrefix))
                key = key.Substring(_bucketPrefix.Length);

            return key.TrimStart(Delimiter.ToCharArray()).TrimEnd(Delimiter.ToCharArray());
        }
    }
}
