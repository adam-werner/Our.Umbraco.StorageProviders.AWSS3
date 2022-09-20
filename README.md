# Our.Umbraco.StorageProviders.AWSS3

The AWS S3 Storage provider has an implementation of the Umbraco `IFileSystem` that connects to an AWS S3 Storage container.

It also has the following features:
- middleware for serving media files from the `/media` path with support for the image cache with files in the `/cache` path
- ImageSharp image provider

### Usage

This provider can be added in the `Startup.cs` file:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddUmbraco(_env, _config)
        .AddBackOffice()
        .AddWebsite()
        .AddComposers()
        // Add the AWS S3 Storage file system
        .AddAWSS3MediaFileSystem()
        .Build();
}

public void Configure(IApplicationBuilder app)
{
    if (env.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

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
            u.UseInstallerEndpoints();
            u.UseBackOfficeEndpoints();
            u.UseWebsiteEndpoints();
        });
}
```

In attempt to pare down the configuration and leverage AWS practices, the settings for this storage rquire only the S3 bucket name in `appsettings.json`:

```json
{
  "Umbraco": {
    "Storage": {
      "AWSS3": {
        "Media": {
          "BucketName": ""
        }
      }
    }
  }
}
```

## Folder structure in the AWS S3 Storage container
With an S3 bucket in place, the `media` folder will contain the traditional seen media folders and files while the `cache` folder will contain the files to support the image cache.

## Bugs, issues and Pull Requests

If you encounter a bug when using this client library please open an issue in the issue tracker of this repository. 

If you are interested in contributing, a Pull Request is always welcome.  Please feel free to open an issue before submitting a Pull Request to discuss what you would like to submit.

## Acknowlegements
This project would not have been possible without the work done by those developers along the fork path.
