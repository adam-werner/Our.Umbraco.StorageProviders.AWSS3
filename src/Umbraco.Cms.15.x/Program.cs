using Our.Umbraco.StorageProviders.AWSS3.DependencyInjection;

namespace Umbraco.Cms._15.x
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var services = builder.Services;
            var configuration = builder.Configuration;
            var env = builder.Environment;

            builder.CreateUmbracoBuilder()
                .AddBackOffice()
                .AddWebsite()
                .AddComposers()
                // Add the AWS S3 Storage file system
                .AddAWSS3MediaFileSystem()
                .Build();

            WebApplication app = builder.Build();

            await app.BootUmbracoAsync();

            app.UseHttpsRedirection();

            app.UseUmbraco()
                .WithMiddleware(u =>
                {
                    u.UseBackOffice();
                    u.UseWebsite();
                    // Enables the AWS S3 Storage middleware for Media
                    u.UseAWSS3MediaFileSystem();
                })
                .WithEndpoints(u =>
                {
                    u.UseBackOfficeEndpoints();
                    u.UseWebsiteEndpoints();
                });

            await app.RunAsync();
        }
    }
}
