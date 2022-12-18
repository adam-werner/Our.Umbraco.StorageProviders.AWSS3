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
using Our.Umbraco.StorageProviders.AWSS3.IO;
using Amazon.Util;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace Our.Umbraco.StorageProviders.AWSS3.Imaging
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

            
            AWSOptions awsOptions = _configuration.GetAWSOptions();
            AWSS3StorageCacheOptions cacheOptions = getAWSS3StorageCacheOptions(fileSystemOptions, awsOptions);

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

            AWSOptions awsOptions = _configuration.GetAWSOptions();
            var cacheOptions = getAWSS3StorageCacheOptions(options, awsOptions);

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

        /// <summary>
        /// Return AWSS3StorageCacheOptions object with logic to check the ProfilesLocation and use that in an override
        /// as the client may want to target an alternative location
        /// </summary>
        /// <param name="awss3FileSystemOptions"></param>
        /// <param name="awsOptions"></param>
        /// <returns></returns>
        private AWSS3StorageCacheOptions getAWSS3StorageCacheOptions(AWSS3FileSystemOptions awss3FileSystemOptions, AWSOptions awsOptions)
        {
            AWSS3StorageCacheOptions cacheOptions = new AWSS3StorageCacheOptions
            {
                BucketName = awss3FileSystemOptions.BucketName,
                Region = getRegionName(awss3FileSystemOptions)
            };

            // if ProfilesLocation added, physical assignment of the AWS credentials must be made
            if (!string.IsNullOrEmpty(awsOptions.ProfilesLocation))
            {
                var chain = new CredentialProfileStoreChain(awsOptions.ProfilesLocation);
                if (chain.TryGetAWSCredentials(awsOptions.Profile, out AWSCredentials awsCredentials))
                {
                    cacheOptions.AccessKey = awsCredentials.GetCredentials().AccessKey;
                    cacheOptions.AccessSecret = awsCredentials.GetCredentials().SecretKey;
                }
            }

            return cacheOptions;
        }
    }
}
