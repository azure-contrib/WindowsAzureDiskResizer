using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ByteSizeLib;
using Microsoft.QualityTools.Testing.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob.Fakes;
using WindowsAzureDiskResizer.Helpers;
using WindowsAzureDiskResizer.Tests.Helpers;

namespace WindowsAzureDiskResizer.Tests
{
    /// <summary>
    /// This unit test class uses Azure Storage Emulator for running unit tests. There is more documentation
    /// about the emulator at: https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator
    /// </summary>
    [TestClass]
    public class ResizeVhdHelperTests
    {
        private const string testDiskUri = "http://127.0.0.1:10000/devstoreaccount1/test-container/TestDisk0.vhd";
        private const string accountName = "devstoreaccount1";
        private const string accountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
        private static readonly Process process;
        private static readonly List<Action> _cleanupActions = new List<Action>();

        /// <summary>
        /// Add a cleanup action to the list of actions to be run in the <see cref="ClassCleanup"/> method.
        /// </summary>
        /// <param name="cleanupAction">An <see cref="Action"/> object.</param>
        public static void AddCleanupAction(Action cleanupAction)
        {
            _cleanupActions.Add(cleanupAction);
        }

        static ResizeVhdHelperTests()
        {
            process = new Process
            {
                StartInfo = {
                    UseShellExecute = false,
                    FileName = @"C:\Program Files (x86)\Microsoft SDKs\Azure\Storage Emulator\AzureStorageEmulator.exe",
                }
            };
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            StartAndWaitForExit("stop");
            StartAndWaitForExit("clear all");
            StartAndWaitForExit("start");
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            foreach (var action in _cleanupActions)
            {
                action();
            }
            StartAndWaitForExit("stop");
        }

        private static void StartAndWaitForExit(string arguments)
        {
            process.StartInfo.Arguments = arguments;
            process.Start();
            process.WaitForExit(10000);
        }

        [TestMethod]
        public void Resize_Vhd_Blob_Not_Exists()
        {
            var newSizeInGb = 1;
            var blobUri = new Uri(testDiskUri);
            var resizeVhdHelper = new ResizeVhdHelper();
            var result = resizeVhdHelper.ResizeVhdBlob(newSizeInGb, blobUri, accountName, accountKey);

            Assert.IsTrue(result == ResizeResult.Error);
        }

        [TestMethod]
        public void Resize_Vhd_Blob_Not_Exists_And_Bytes_Are_The_Same()
        {
            var newSizeInGb = 1;
            var blobUri = new Uri(testDiskUri);
            var resizeVhdHelper = new ResizeVhdHelper();
            var result = resizeVhdHelper.ResizeVhdBlob(newSizeInGb, blobUri, accountName, accountKey);

            Assert.IsTrue(result == ResizeResult.Error);
            Assert.AreEqual(ByteSize.FromGigaBytes(newSizeInGb).Bytes, resizeVhdHelper.NewSize.Bytes);
        }

        [TestMethod]
        public void Resize_Vhd_Blob_Fail()
        {
            using (ShimsContext.Create())
            {
                var newSizeInGb = 1;
                var blobUri = new Uri(testDiskUri);
                ShimCloudBlob.AllInstances.ExistsBlobRequestOptionsOperationContext = (blob, opts, context) =>
                {
                    throw new StorageException();
                };

                var resizeVhdHelper = new ResizeVhdHelper();
                var result = resizeVhdHelper.ResizeVhdBlob(newSizeInGb, blobUri, accountName, accountKey);
                Assert.IsTrue(result == ResizeResult.Error);
            }
        }

        [TestMethod]
        public void Resize_Vhd_Blob_Empty_Account_Details()
        {
            var newSizeInGb = 1;
            var blobUri = new Uri(testDiskUri);
            var resizeVhdHelper = new ResizeVhdHelper();
            var result = resizeVhdHelper.ResizeVhdBlob(newSizeInGb, blobUri, null, null);
            Assert.IsTrue(result == ResizeResult.Error);
        }

        [TestMethod]
        public void Resize_Vhd_Blob_Dynamic_Disk()
        {
            var newSizeInGb = 1;
            var vhdFilePath = "TestDisk-Dynamic.vhd";
            var containerName = "test-container";

            // First create the dynamic VHD file
            VhdHelper.CreateVhdDisk(true, newSizeInGb, vhdFilePath, "Testing Disk");
            var vhdBlobUri = AzureStorageEmulatorHelper.UploadVhdFileToContainer(containerName, vhdFilePath);

            // Then resize the VHD file
            var resizeVhdHelper = new ResizeVhdHelper();
            var result = resizeVhdHelper.ResizeVhdBlob(newSizeInGb, vhdBlobUri, accountName, accountKey);
            var length = AzureStorageEmulatorHelper.GetVhdSizeInContainer(vhdBlobUri);

            // Clean the files in the container and the local file system
            AddCleanupAction(() => AzureStorageEmulatorHelper.DeleteVhdFileInContainer(containerName, vhdBlobUri));
            AddCleanupAction(() => File.Delete(vhdFilePath));

            Assert.IsTrue(result == ResizeResult.Error);
            Assert.IsTrue(ByteSize.FromGigaBytes(newSizeInGb) != ByteSize.FromBytes(length));
        }

        [TestMethod]
        public void Resize_Vhd_Blob_Shrink()
        {
            var firstSizeInGb = 2;
            var newSizeInGb = 1;
            var vhdFilePath = "TestDisk-Shrink.vhd";
            var containerName = "test-container";

            // First create the fixed VHD file
            VhdHelper.CreateVhdDisk(false, firstSizeInGb, vhdFilePath, "Testing Shrink Disk");
            var vhdBlobUri = AzureStorageEmulatorHelper.UploadVhdFileToContainer(containerName, vhdFilePath);

            // Then resize the VHD file
            var resizeVhdHelper = new ResizeVhdHelper();
            var firstResult = resizeVhdHelper.ResizeVhdBlob(newSizeInGb, vhdBlobUri, accountName, accountKey);
            var finalResult = ResizeResult.Error;
            if (firstResult == ResizeResult.Shrink)
            {
                resizeVhdHelper.IsExpand = false;
                finalResult = resizeVhdHelper.DoResizeVhdBlob();
            }
            var length = AzureStorageEmulatorHelper.GetVhdSizeInContainer(vhdBlobUri);

            // Clean the files in the container and the local file system
            AddCleanupAction(() => AzureStorageEmulatorHelper.DeleteVhdFileInContainer(containerName, vhdBlobUri));
            AddCleanupAction(() => File.Delete(vhdFilePath));

            Assert.IsTrue(firstResult == ResizeResult.Shrink);
            Assert.IsTrue(finalResult == ResizeResult.Success);
            Assert.IsTrue(newSizeInGb == (int)ByteSize.FromBytes(length).GigaBytes);
        }

        [TestMethod]
        public void Resize_Vhd_Blob_Expand()
        {
            var firstSizeInGb = 1;
            var newSizeInGb = 2;
            var vhdFilePath = "TestDisk-Expand.vhd";
            var containerName = "test-container";

            // First create the fixed VHD file
            VhdHelper.CreateVhdDisk(false, firstSizeInGb, vhdFilePath, "Testing Expand Disk");
            var vhdBlobUri = AzureStorageEmulatorHelper.UploadVhdFileToContainer(containerName, vhdFilePath);

            // Then resize the VHD file
            var resizeVhdHelper = new ResizeVhdHelper();
            var result = resizeVhdHelper.ResizeVhdBlob(newSizeInGb, vhdBlobUri, accountName, accountKey);
            var length = AzureStorageEmulatorHelper.GetVhdSizeInContainer(vhdBlobUri);

            // Clean the files in the container and the local file system
            AddCleanupAction(() => AzureStorageEmulatorHelper.DeleteVhdFileInContainer(containerName, vhdBlobUri));
            AddCleanupAction(() => File.Delete(vhdFilePath));

            Assert.IsTrue(result == ResizeResult.Success);
            Assert.IsTrue(newSizeInGb == (int)ByteSize.FromBytes(length).GigaBytes);
        }
    }
}
