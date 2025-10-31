namespace Our.Umbraco.StorageProviders.AWSS3.Services
{
    public interface IMimeTypeResolver
    {
        string Resolve(string filename);
    }
}
