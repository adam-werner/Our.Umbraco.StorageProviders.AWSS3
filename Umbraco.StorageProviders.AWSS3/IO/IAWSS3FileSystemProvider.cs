using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Our.Umbraco.StorageProviders.AWSS3.IO
{
    public interface IAWSS3FileSystemProvider
    {
        /// <summary>
        /// Get the file system by its <paramref name="name" />.
        /// </summary>
        /// <param name="name">The name of the <see cref="IAWSS3FileSystem" />.</param>
        /// <returns>
        /// The <see cref="IAWSS3FileSystem" />.
        /// </returns>
        IAWSS3FileSystem GetFileSystem(string name);
    }
}
