using Amazon.S3;
using Umbraco.Cms.Core.IO;

namespace Umbraco.StorageProviders.AWSS3.IO
{
    public interface IAWSS3FileSystem : IFileSystem
    {
        /// <summary>
        /// Get the <see cref="S3Client"/>.
        /// </summary>
        /// <param name="path">The relative path to the blob.</param>
        /// <returns>A <see cref="S3Client"/></returns>
        IAmazonS3 GetS3Client(string path);

        string ResolveBucketPath(string path, bool isDir = false);
    }
}
