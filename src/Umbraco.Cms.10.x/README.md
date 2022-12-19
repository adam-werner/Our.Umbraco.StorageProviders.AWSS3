## Demo Umbraco website
This is a baseline installation of Umbraco v10.0 to assist in the development the Our.Umbraco.StorageProviders.AWSS3 package as well as an opportunity to provide an example of how this package can be setup in an Umbraco website.

Please note that this is a non-production site and setup decisions were made with that idea in mind.

### Installation
The `appsettings.json` is setup to perform an unattended installation for a SQLite database.  The credentials to gain access to the Umbraco backoffice are 

Username: `test@test.com`
Password: `test123456`

### S3 configuration
The configuration of an S3 bucket can be seen in the overall solutions readme file here -- [Our.Umbraco.StorageProviders.AWSS3 readme](../../README.md)