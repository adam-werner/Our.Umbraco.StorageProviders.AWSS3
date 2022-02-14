using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.StaticFiles;

namespace Umbraco.StorageProviders.AWSS3.Services
{
    class MimeTypeResolver : IMimeTypeResolver
    {
        public string Resolve(string filename)
        {
            string contentType;
            new FileExtensionContentTypeProvider().TryGetContentType(filename, out contentType);
            return contentType ?? "application/octet-stream";
        }
    }
}
