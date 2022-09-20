using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web;
using SixLabors.ImageSharp.Web.Caching;
using SixLabors.ImageSharp.Web.Caching.AWS;
using SixLabors.ImageSharp.Web.Resolvers;
using Umbraco.StorageProviders.AWSS3.IO;

namespace Umbraco.StorageProviders.AWSS3.Imaging
{
    /// <summary>
    /// Implements an S3 based cache storing files in a <c>cache</c> subfolder.
    /// </summary>
    public class AWSS3FileSystemImageCache : IImageCache
    {
        private const string _cachePath = "cache/";
        private readonly string _name;
        private AWSS3StorageCache baseCache = null;
        private readonly IConfiguration _configuration;
        private readonly IAmazonS3 _s3Client;

        /// <summary>
        /// Initializes a new instance of the <see cref="AWSS3FileSystemImageCache" /> class.
        /// </summary>
        /// <param name="options">The options.</param>
        public AWSS3FileSystemImageCache(IOptionsMonitor<AWSS3FileSystemOptions> options, IConfiguration configuration, IAmazonS3 s3Client)
            : this(AWSS3FileSystemOptions.MediaFileSystemName, options, configuration, s3Client)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AWSS3FileSystemImageCache" />.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <exception cref="System.ArgumentNullException">options
        /// or
        /// name</exception>
        protected AWSS3FileSystemImageCache(string name, IOptionsMonitor<AWSS3FileSystemOptions> options, IConfiguration configuration, IAmazonS3 s3Client)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));

            if (options == null) throw new ArgumentNullException(nameof(options));

            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            _configuration = configuration;

            _s3Client = s3Client;


            // Collect configurations
            var fileSystemOptions = options.Get(name);

            // Bucket name comes from the AWSFileSystemOptions - BucketName is required in appsettings
            string bucketName = fileSystemOptions.BucketName;

            // Get region -- start with fileSystemOptions; Doesn't exist? fallback to AWSOptions and then to S3Client
            AWSOptions awsOptions = _configuration.GetAWSOptions();
            string region = getRegionName(fileSystemOptions);

            var cacheOptions = new AWSS3StorageCacheOptions
            {
                BucketName = bucketName,
                Region = region
            };

            baseCache = new AWSS3StorageCache(Options.Create(cacheOptions));

            options.OnChange(OptionsOnChange);
        }

        /// <inheritdoc/>
        public async Task<IImageCacheResolver> GetAsync(string key)
        {
            string cacheAndKey = Path.Combine(_cachePath, key);

            return await baseCache.GetAsync(cacheAndKey);
        }

        /// <inheritdoc/>
        public Task SetAsync(string key, Stream stream, ImageCacheMetadata metadata)
        {
            string cacheAndKey = Path.Combine(_cachePath, key);
            return baseCache.SetAsync(cacheAndKey, stream, metadata);
        }


        private void OptionsOnChange(AWSS3FileSystemOptions options, string name)
        {
            if (name != _name) return;

            var cacheOptions = new AWSS3StorageCacheOptions
            {
                BucketName = options.BucketName,
                Region = getRegionName(options)
            };

            baseCache = new AWSS3StorageCache(Options.Create(cacheOptions));
        }


        private string getRegionName(AWSS3FileSystemOptions options)
        {
            // Get region -- start with fileSystemOptions; Doesn't exist? fallback to AWSOptions and then to S3Client
            string region = options.Region;
            if (string.IsNullOrEmpty(region))
            {
                AWSOptions awsOptions = _configuration.GetAWSOptions();
                if (awsOptions != null && awsOptions.Region != null)
                {
                    region = awsOptions.Region.SystemName;
                }
                else if (_s3Client != null)
                {
                    region = _s3Client.Config?.RegionEndpoint?.SystemName;
                }
            }

            return region;
        }
    }
}
