# Umbraco.StorageProviders

This repository contains Umbraco storage providers that can replace the default physical file storage.

## Umbraco.StorageProviders.AWSS3

The AWS S3 Storage provider has an implementation of the Umbraco `IFileSystem` that connects to an AWS S3 Storage container.

It also has the following features:
- middleware for serving media files from the `/media` path
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
        // Add the AWS S3 Storage file system, ImageSharp image provider/cache and middleware for Media:
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
            // Enables the AWS S3 Storage middleware for Media:
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

There're multiple ways to configure the provider, it can be done in code:

```csharp
services.AddUmbraco(_env, _config)

    .AddAWSS3MediaFileSystem(options
        options.BucketName = ""; => {
        options.Region = "";
        options.BucketPrefix = "";
    })

```

In `appsettings.json`:

```json
{
  "Umbraco": {
    "Storage": {
      "AWSS3": {
        "Media": {
          "BucketName": "",
          "Region": "",
          "BucketPrefix": ""
        }
      }
    }
  }
}
```

Or by environment variables:

```sh
UMBRACO__STORAGE__AWSS3__MEDIA__BUCKETNAME=<BUCKET_NAME>
UMBRACO__STORAGE__AWSS3__MEDIA__REGION=<REGION>
UMBRACO__STORAGE__AWSS3__MEDIA__BUCKETPREFIX=<BUCKETPREFIX>
```

_Note: you still have to add the provider in the `Startup.cs` file when not configuring the options in code._

## Folder structure in the AWS S3 Storage container
The container name is expected to exist and uses the following folder structure:
- `/media` - contains the Umbraco media, stored in the structure defined by the `IMediaPathScheme` registered in Umbraco (the default `UniqueMediaPathScheme` stores files with their original filename in 8 character directories, based on the content and property GUID identifier)

Note that this is different than the behavior of other file system providers - i.e. https://github.com/andyfelton-equatedigital/Umbraco.StorageProviders.AWSS3 that expect the media contents to be at the root level.

## Bugs, issues and Pull Requests

If you encounter a bug when using this client library you are welcome to open an issue in the issue tracker of this repository. We always welcome Pull Request and please feel free to open an issue before submitting a Pull Request to discuss what you want to submit.

## License

Umbraco Storage Providers is [MIT licensed](LICENSE).