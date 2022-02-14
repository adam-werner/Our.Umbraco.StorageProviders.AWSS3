using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web.Caching;
using SixLabors.ImageSharp.Web.Caching.AWS;
using SixLabors.ImageSharp.Web.Providers;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Umbraco.Extensions;
using Umbraco.StorageProviders.AWSS3.Imaging;
using Umbraco.StorageProviders.AWSS3.IO;
using Umbraco.StorageProviders.AWSS3.Services;

namespace Umbraco.StorageProviders.AWSS3.DependencyInjection
{
    public static class AWSS3MediaFileSystemExtensions
    {
        /// <summary>
        /// Registers an <see cref="IAWSS3FileSystem" /> and it's dependencies configured for media.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoBuilder" />.</param>
        /// <returns>
        /// The <see cref="IUmbracoBuilder" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">builder</exception>
        public static IUmbracoBuilder AddAWSS3MediaFileSystem(this IUmbracoBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.AddAWSS3FileSystem(AWSS3FileSystemOptions.MediaFileSystemName, "~/media",
                (options, provider) =>
                {
                    var globalSettingsOptions = provider.GetRequiredService<IOptions<GlobalSettings>>();
                    options.VirtualPath = globalSettingsOptions.Value.UmbracoMediaPath;
                });

            builder.Services.TryAddSingleton<AWSS3FileSystemMiddleware>();

            // ImageSharp image provider/cache
            builder.Services.Insert(0, ServiceDescriptor.Singleton<IImageProvider, AWSS3FileSystemImageProvider>());
            builder.Services.AddUnique<IImageCache, AWSS3FileSystemImageCache>();

            builder.SetMediaFileSystem(provider => provider.GetRequiredService<IAWSS3FileSystemProvider>()
                .GetFileSystem(AWSS3FileSystemOptions.MediaFileSystemName));

            return builder;
        }
        /// <summary>
        /// Registers a <see cref="IAWSS3FileSystem" /> and it's dependencies configured for media.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoBuilder" />.</param>
        /// <param name="configure">An action used to configure the <see cref="AWSS3FileSystemOptions" />.</param>
        /// <returns>
        /// The <see cref="IUmbracoBuilder" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">builder
        /// or
        /// configure</exception>
        public static IUmbracoBuilder AddAWSS3MediaFileSystem(this IUmbracoBuilder builder, Action<AWSS3FileSystemOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            AddAWSS3MediaFileSystem(builder);

            builder.Services
                .AddOptions<AWSS3FileSystemOptions>(AWSS3FileSystemOptions.MediaFileSystemName)
                .Configure(configure);

            return builder;
        }

        /// <summary>
        /// Registers a <see cref="IAWSS3FileSystem" /> and it's dependencies configured for media.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoBuilder" />.</param>
        /// <param name="configure">An action used to configure the <see cref="AWSS3FileSystemOptions" />.</param>
        /// <returns>
        /// The <see cref="IUmbracoBuilder" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">builder
        /// or
        /// configure</exception>
        public static IUmbracoBuilder AddAWSS3MediaFileSystem(this IUmbracoBuilder builder, Action<AWSS3FileSystemOptions, IServiceProvider> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            AddAWSS3MediaFileSystem(builder);

            builder.Services
                .AddOptions<AWSS3FileSystemOptions>(AWSS3FileSystemOptions.MediaFileSystemName)
                .Configure(configure);

            return builder;
        }

        /// <summary>
        /// Adds the <see cref="AWSS3FileSystemMiddleware" />.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoApplicationBuilderContext" />.</param>
        /// <returns>
        /// The <see cref="IUmbracoApplicationBuilderContext" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">builder</exception>
        public static IUmbracoApplicationBuilderContext UseAWSS3MediaFileSystem(this IUmbracoApplicationBuilderContext builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            UseAWSS3MediaFileSystem(builder.AppBuilder);

            return builder;
        }

        /// <summary>
        /// Adds the <see cref="AWSS3FileSystemMiddleware" />.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder" />.</param>
        /// <returns>
        /// The <see cref="IApplicationBuilder" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">app</exception>
        public static IApplicationBuilder UseAWSS3MediaFileSystem(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));

            app.UseMiddleware<AWSS3FileSystemMiddleware>();

            return app;
        }
    }
}
