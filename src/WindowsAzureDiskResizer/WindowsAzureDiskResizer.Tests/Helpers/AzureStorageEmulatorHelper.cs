using System;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace WindowsAzureDiskResizer.Tests.Helpers
{
    /// <summary>
    /// This helper has methods for accomplishing actions with the Azure Storage Emulator.
    /// </summary>
    public class AzureStorageEmulatorHelper
    {
        /// <summary>
        /// Uploads the VHD file specified as a <see cref="CloudPageBlob"/> object, using the parameters specified.
        /// </summary>
        /// <param name="containerName">The container in the Azure Storage account where the VHD file will be uploaded to.</param>
        /// <param name="filePath">The path of the VHD file.</param>
        /// <param name="useDevelopment">True to use the Azure Storage Emulator, False to connect to the storage account using the other parameters.</param>
        /// <param name="accountName">The name of the account to use, if useDevelopment is false.</param>
        /// <param name="accountKey">The key of the account to use, if useDevelopment is false.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Returns the size of the VHD file specified, in bytes.
        /// </summary>
        /// <param name="vhdFileUri">The location of the VHD file in the storage account, as a <see cref="Uri"/> object.</param>
        /// <param name="useDevelopment">True to use the Azure Storage Emulator, False to connect to the storage account using the other parameters.</param>
        /// <param name="accountName">The name of the account to use, if useDevelopment is false.</param>
        /// <param name="accountKey">The key of the account to use, if useDevelopment is false.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Deletes the VHD file specified along with its container from the storage account.
        /// </summary>
        /// <param name="containerName">The container in the Azure Storage account where the VHD file is located.</param>
        /// <param name="vhdFileUri">The location of the VHD file in the storage account, as a <see cref="Uri"/> object.</param>
        /// <param name="useDevelopment">True to use the Azure Storage Emulator, False to connect to the storage account using the other parameters.</param>
        /// <param name="accountName">The name of the account to use, if useDevelopment is false.</param>
        /// <param name="accountKey">The key of the account to use, if useDevelopment is false.</param>
        public static void DeleteVhdFileInContainer(string containerName, Uri vhdFileUri, bool useDevelopment = true, string accountName = null, string accountKey = null)
        {
            var account = GetStorageAccount(useDevelopment, accountName, accountKey);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);
            var blob = new CloudPageBlob(vhdFileUri, client);

            blob.DeleteIfExists();
            container.DeleteIfExists();
        }

        /// <summary>
        /// Returns the <see cref="CloudStorageAccount"/> object based on the parameters specified.
        /// </summary>
        /// <param name="useDevelopment">True to use the Azure Storage Emulator, False to connect to the storage account using the other parameters.</param>
        /// <param name="accountName">The name of the account to use, if useDevelopment is false.</param>
        /// <param name="accountKey">The key of the account to use, if useDevelopment is false.</param>
        /// <returns></returns>
        private static CloudStorageAccount GetStorageAccount(bool useDevelopment, string accountName, string accountKey)
        {
            if (useDevelopment)
                return CloudStorageAccount.DevelopmentStorageAccount;
            else
                return new CloudStorageAccount(new StorageCredentials(accountName, accountKey), true);
        }
    }
}
