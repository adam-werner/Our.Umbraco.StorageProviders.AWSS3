using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.StorageProviders.AWSS3.IO;
using Umbraco.StorageProviders.AWSS3.Services;

// ReSharper disable once CheckNamespace
// uses same namespace as Umbraco Core for easier discoverability
namespace Umbraco.Cms.Core.DependencyInjection
{
    /// <summary>
    /// Extension methods to help registering AWS S3 file systems.
    /// </summary>
    public static class AWSS3FileSystemExtensions
    {
        /// <summary>
        /// Registers a <see cref="IAWSS3FileSystem" /> in the <see cref="IServiceCollection" />, with it's configuration
        /// loaded from <c>Umbraco:Storage:AWSS3:{name}</c> where {name} is the value of the <paramref name="name" /> parameter.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoBuilder" />.</param>
        /// <param name="name">The name of the file system.</param>
        /// <param name="path">The path to map the filesystem to.</param>
        /// <returns>
        /// The <see cref="IUmbracoBuilder" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">builder
        /// or
        /// name</exception>
        /// <exception cref="System.ArgumentException">Value cannot be null or whitespace. - path</exception>
        public static IUmbracoBuilder AddAWSS3FileSystem(this IUmbracoBuilder builder, string name, string path)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));

            builder.Services.TryAddSingleton<IMimeTypeResolver, MimeTypeResolver>();
            builder.Services.TryAddSingleton<IAWSS3FileSystemProvider, AWSS3FileSystemProvider>();

            builder.Services
                .AddOptions<AWSS3FileSystemOptions>(name)
                .BindConfiguration($"Umbraco:Storage:AWSS3:{name}")
                .Configure(options => options.VirtualPath = path)
                .ValidateDataAnnotations();

            return builder;
        }

        /// <summary>
        /// Registers a <see cref="IAWSS3FileSystem" /> in the <see cref="IServiceCollection" />, with it's configuration
        /// loaded from <c>Umbraco:Storage:AWSS3:{name}</c> where {name} is the value of the <paramref name="name" /> parameter.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoBuilder" />.</param>
        /// <param name="name">The name of the file system.</param>
        /// <param name="path">The path to map the filesystem to.</param>
        /// <param name="configure">An action used to configure the <see cref="AWSS3FileSystemOptions" />.</param>
        /// <returns>
        /// The <see cref="IUmbracoBuilder" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">builder
        /// or
        /// name
        /// or
        /// configure</exception>
        /// <exception cref="System.ArgumentException">Value cannot be null or whitespace. - path</exception>
        public static IUmbracoBuilder AddAWSS3FileSystem(this IUmbracoBuilder builder, string name, string path, Action<AWSS3FileSystemOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            AddAWSS3FileSystem(builder, name, path);

            builder.Services
                .AddOptions<AWSS3FileSystemOptions>(name)
                .Configure(configure);

            return builder;
        }

        /// <summary>
        /// Registers a <see cref="IAWSS3FileSystem" /> in the <see cref="IServiceCollection" />, with it's configuration
        /// loaded from <c>Umbraco:Storage:AWSS3:{name}</c> where {name} is the value of the <paramref name="name" /> parameter.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoBuilder" />.</param>
        /// <param name="name">The name of the file system.</param>
        /// <param name="path">The path to map the filesystem to.</param>
        /// <param name="configure">An action used to configure the <see cref="AWSS3FileSystemOptions" />.</param>
        /// <returns>
        /// The <see cref="IUmbracoBuilder" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">builder
        /// or
        /// name
        /// or
        /// configure</exception>
        /// <exception cref="System.ArgumentException">Value cannot be null or whitespace. - path</exception>
        public static IUmbracoBuilder AddAWSS3FileSystem(this IUmbracoBuilder builder, string name, string path, Action<AWSS3FileSystemOptions, IServiceProvider> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            AddAWSS3FileSystem(builder, name, path);

            builder.Services
                .AddOptions<AWSS3FileSystemOptions>(name)
                .Configure(configure);

            return builder;
        }
    }
}
