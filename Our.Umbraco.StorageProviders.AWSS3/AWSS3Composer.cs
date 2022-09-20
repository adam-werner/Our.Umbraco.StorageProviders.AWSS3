using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Our.Umbraco.StorageProviders.AWSS3
{
    public class AWSS3Composer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            AWSOptions awsOptions = builder.Config.GetAWSOptions();

            builder.Services.AddDefaultAWSOptions(awsOptions);
            builder.Services.AddAWSService<IAmazonS3>();
        }
    }
}
