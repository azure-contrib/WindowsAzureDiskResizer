using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ByteSizeLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowsAzureDiskResizer.Helpers;
using WindowsAzureDiskResizer.Tests.Helpers;

namespace WindowsAzureDiskResizer.Tests
{
    [TestClass]
    public class ResizeVhdHelperTests
    {
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
            var blobUri = new Uri("http://127.0.0.1:10000/devstoreaccount1/test-container/TestDisk0.vhd");
            var accountName = "devstoreaccount1";
            var accountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

            var resizeVhdHelper = new ResizeVhdHelper();
            var result = resizeVhdHelper.ResizeVhdBlob(newSizeInGb, blobUri, accountName, accountKey);

            Assert.IsTrue(result == ResizeResult.Error);
        }

        [TestMethod]
        public void Resize_Vhd_Blob_Not_Exists_And_Bytes_Are_The_Same()
        {
            var newSizeInGb = 1;
            var blobUri = new Uri("http://127.0.0.1:10000/devstoreaccount1/test-container/TestDisk0.vhd");
            var accountName = "devstoreaccount1";
            var accountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

            var resizeVhdHelper = new ResizeVhdHelper();
            var result = resizeVhdHelper.ResizeVhdBlob(newSizeInGb, blobUri, accountName, accountKey);

            Assert.IsTrue(result == ResizeResult.Error);
            Assert.AreEqual(ByteSize.FromGigaBytes(newSizeInGb).Bytes, resizeVhdHelper.NewSize.Bytes);
        }

        [TestMethod]
        public void Resize_Vhd_Blob_Dynamic_Disk()
        {
            var newSizeInGb = 1;
            var accountName = "devstoreaccount1";
            var accountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
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
            var newSizeInGb = 1;
            var firstSizeInGb = 2;
            var accountName = "devstoreaccount1";
            var accountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
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
            var newSizeInGb = 2;
            var firstSizeInGb = 1;
            var accountName = "devstoreaccount1";
            var accountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
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
