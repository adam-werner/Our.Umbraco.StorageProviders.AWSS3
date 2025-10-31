using Microsoft.AspNetCore.StaticFiles;

namespace Our.Umbraco.StorageProviders.AWSS3.Services
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
