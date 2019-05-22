using System;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace WindowsAzureDiskResizer.Tests.Helpers
{
    public class AzureStorageEmulatorHelper
    {
        public static Uri UploadVhdFileToContainer(string containerName, string filePath, bool useDevelopment = true, string accountName = null, string accountKey = null)
        {
            var account = GetStorageAccount(useDevelopment, accountName, accountKey);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);
            container.CreateIfNotExists();

            var fileName = Path.GetFileName(filePath);
            CloudPageBlob blob = container.GetPageBlobReference(fileName);
            blob.UploadFromFile(filePath);
            return blob.Uri;
        }

        public static long GetVhdSizeInContainer(Uri vhdFileUri, bool useDevelopment = true, string accountName = null, string accountKey = null)
        {
            var account = GetStorageAccount(useDevelopment, accountName, accountKey);
            var client = account.CreateCloudBlobClient();
            var blob = new CloudPageBlob(vhdFileUri, client);

            if (blob.Exists())
            {
                blob.FetchAttributes();
                return blob.Properties.Length;
            }
            else
                return 0L;
        }

        public static void DeleteVhdFileInContainer(string containerName, Uri vhdFileUri, bool useDevelopment = true, string accountName = null, string accountKey = null)
        {
            var account = GetStorageAccount(useDevelopment, accountName, accountKey);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);
            var blob = new CloudPageBlob(vhdFileUri, client);

            blob.DeleteIfExists();
            container.DeleteIfExists();
        }

        private static CloudStorageAccount GetStorageAccount(bool useDevelopment, string accountName, string accountKey)
        {
            if (useDevelopment)
                return CloudStorageAccount.DevelopmentStorageAccount;
            else
                return new CloudStorageAccount(new StorageCredentials(accountName, accountKey), true);
        }
    }
}
