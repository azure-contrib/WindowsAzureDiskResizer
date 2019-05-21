using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowsAzureDiskResizer.Helpers;

namespace WindowsAzureDiskResizer.Tests
{
    [TestClass]
    public class ResizeVhdHelperTest
    {
        private static readonly Process process;

        static ResizeVhdHelperTest()
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
            StartAndWaitForExit("start");
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
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
            Assert.AreEqual(newSizeInGb * 1024 * 1024 * 1024, resizeVhdHelper.NewSize.Bytes);
        }

        [TestMethod]
        public void Resize_Vhd_Blob_Dynamic_Disk()
        {
            var newSizeInGb = 1;
            var blobUri = new Uri("http://127.0.0.1:10000/devstoreaccount1/test-container/TestDisk_Dynamic.vhd");
            var accountName = "devstoreaccount1";
            var accountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

            var resizeVhdHelper = new ResizeVhdHelper();
            var result = resizeVhdHelper.ResizeVhdBlob(newSizeInGb, blobUri, accountName, accountKey);

            Assert.IsTrue(result == ResizeResult.Error);
        }

        [TestMethod]
        public void Resize_Vhd_Blob_Shrink()
        {
            var newSizeInGb = 1;
            var blobUri = new Uri("http://127.0.0.1:10000/devstoreaccount1/test-container/TestDisk_Shrink.vhd");
            var accountName = "devstoreaccount1";
            var accountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

            var resizeVhdHelper = new ResizeVhdHelper();
            var firstResult = resizeVhdHelper.ResizeVhdBlob(newSizeInGb, blobUri, accountName, accountKey);
            var finalResult = ResizeResult.Error;
            if (firstResult == ResizeResult.Shrink)
            {
                resizeVhdHelper.IsExpand = false;
                finalResult = resizeVhdHelper.DoResizeVhdBlob();
            }

            Assert.IsTrue(firstResult == ResizeResult.Shrink);
            Assert.IsTrue(finalResult == ResizeResult.Success);
        }

        [TestMethod]
        public void Resize_Vhd_Blob_Expand()
        {
            var newSizeInGb = 2;
            var blobUri = new Uri("http://127.0.0.1:10000/devstoreaccount1/test-container/TestDisk_Expand.vhd");
            var accountName = "devstoreaccount1";
            var accountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

            var resizeVhdHelper = new ResizeVhdHelper();
            var result = resizeVhdHelper.ResizeVhdBlob(newSizeInGb, blobUri, accountName, accountKey);

            Assert.IsTrue(result == ResizeResult.Success);
        }
    }
}
