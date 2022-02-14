using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;
using Umbraco.Extensions;
using Umbraco.StorageProviders.AWSS3.Services;

namespace Umbraco.StorageProviders.AWSS3.IO
{
    class AWSS3FileSystemProvider : IAWSS3FileSystemProvider
    {
        private readonly ConcurrentDictionary<string, IAWSS3FileSystem> _fileSystems = new();
        private readonly IOptionsMonitor<AWSS3FileSystemOptions> _optionsMonitor;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IIOHelper _ioHelper;
        private readonly ILoggerFactory _loggerFactory;
        private readonly FileExtensionContentTypeProvider _fileExtensionContentTypeProvider;
        private readonly IMimeTypeResolver _mimeTypeResolver;

        /// <summary>
        /// Creates a new instance of <see cref="AzureBlobFileSystemProvider" />.
        /// </summary>
        /// <param name="optionsMonitor">The options monitor.</param>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <param name="ioHelper">The IO helper.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="System.ArgumentNullException">optionsMonitor
        /// or
        /// hostingEnvironment
        /// or
        /// ioHelper</exception>
        public AWSS3FileSystemProvider(IOptionsMonitor<AWSS3FileSystemOptions> optionsMonitor, IHostingEnvironment hostingEnvironment, IIOHelper ioHelper,
            ILoggerFactory loggerFactory, IMimeTypeResolver mimeTypeResolver)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _hostingEnvironment = hostingEnvironment ?? throw new ArgumentNullException(nameof(hostingEnvironment));
            _ioHelper = ioHelper ?? throw new ArgumentNullException(nameof(ioHelper));
            _loggerFactory = loggerFactory;
            _mimeTypeResolver = mimeTypeResolver;

            _fileExtensionContentTypeProvider = new FileExtensionContentTypeProvider();
            _optionsMonitor.OnChange(OptionsOnChange);
        }

        /// <inheritdoc />
        public IAWSS3FileSystem GetFileSystem(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            return _fileSystems.GetOrAdd(name, CreateInstance);
        }

        private IAWSS3FileSystem CreateInstance(string name)
        {
            var options = _optionsMonitor.Get(name);

            return CreateInstance(options);
        }

        private IAWSS3FileSystem CreateInstance(AWSS3FileSystemOptions options)
        {
            return new AWSS3FileSystem(options, _hostingEnvironment, _ioHelper, _fileExtensionContentTypeProvider, 
                _loggerFactory.CreateLogger<AWSS3FileSystem>(), _mimeTypeResolver);
        }

        private void OptionsOnChange(AWSS3FileSystemOptions options, string name)
        {
            _fileSystems.TryUpdate(name, _ => CreateInstance(options));
        }
    }
}
