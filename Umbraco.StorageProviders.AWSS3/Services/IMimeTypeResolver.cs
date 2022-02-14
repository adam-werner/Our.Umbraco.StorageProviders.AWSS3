using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Umbraco.StorageProviders.AWSS3.Services
{
    public interface IMimeTypeResolver
    {
        string Resolve(string filename);
    }
}
