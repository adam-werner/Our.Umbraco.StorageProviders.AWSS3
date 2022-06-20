using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web;
using SixLabors.ImageSharp.Web.Caching;
using SixLabors.ImageSharp.Web.Caching.AWS;
using SixLabors.ImageSharp.Web.Providers.AWS;
using SixLabors.ImageSharp.Web.Resolvers;
using SixLabors.ImageSharp.Web.Resolvers.AWS;
using Umbraco.StorageProviders.AWSS3.IO;

namespace Umbraco.StorageProviders.AWSS3.Imaging
{
    /// <summary>
    /// Implements an Azure Blob Storage based cache storing files in a <c>cache</c> subfolder.
    /// </summary>
    public class AWSS3FileSystemImageCache : IImageCache
    {
        private const string _cachePath = "cache/";
        private readonly string _name;
        private AWSS3StorageCache baseCache = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="AWSS3FileSystemImageCache" /> class.
        /// </summary>
        /// <param name="options">The options.</param>
        public AWSS3FileSystemImageCache(IOptionsMonitor<AWSS3FileSystemOptions> options)
            : this(AWSS3FileSystemOptions.MediaFileSystemName, options)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AzureBlobFileSystemImageCache" />.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <exception cref="System.ArgumentNullException">options
        /// or
        /// name</exception>
        protected AWSS3FileSystemImageCache(string name, IOptionsMonitor<AWSS3FileSystemOptions> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _name = name ?? throw new ArgumentNullException(nameof(name));

            var fileSystemOptions = options.Get(name);

            var cacheOptions = new AWSS3StorageCacheOptions
            {
                BucketName = fileSystemOptions.BucketName,
                Region = fileSystemOptions.Region,

            };

            baseCache = new AWSS3StorageCache(Options.Create(cacheOptions));

            options.OnChange(OptionsOnChange);
        }

        /// <inheritdoc/>
        public async Task<IImageCacheResolver> GetAsync(string key)
        {
            return await baseCache.GetAsync(key);
        }

        /// <inheritdoc/>
        public Task SetAsync(string key, Stream stream, ImageCacheMetadata metadata)
        {
            return baseCache.SetAsync(key, stream, metadata);
        }


        private void OptionsOnChange(AWSS3FileSystemOptions options, string name)
        {
            if (name != _name) return;

            var cacheOptions = new AWSS3StorageCacheOptions
            {
                BucketName = options.BucketName,
                Region = options.Region
            };

            baseCache = new AWSS3StorageCache(Options.Create(cacheOptions));
        }
    }
}
