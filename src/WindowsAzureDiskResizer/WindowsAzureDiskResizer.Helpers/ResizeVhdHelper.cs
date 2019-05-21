using System;
using System.IO;
using ByteSizeLib;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using WindowsAzureDiskResizer.Helpers.DiscUtils;

namespace WindowsAzureDiskResizer.Helpers
{
    public class ResizeVhdHelper
    {
        private CloudPageBlob blob;
        private byte[] footer = new byte[512];
        private Footer footerInstance;
        private long originalLength;

        public bool IsExpand { get; set; } = true;

        public ByteSize NewSize { get; set; }

        public ResizeResult ResizeVhdBlob(int newSizeInGb, Uri blobUri, string accountName, string accountKey)
        {
            NewSize = ByteSize.FromGigaBytes(newSizeInGb);

            // Check if blob exists
            blob = new CloudPageBlob(blobUri);
            if (!string.IsNullOrEmpty(accountName) && !string.IsNullOrEmpty(accountKey))
            {
                blob = new CloudPageBlob(blobUri, new StorageCredentials(accountName, accountKey));
            }
            try
            {
                if (!blob.Exists())
                {
                    Console.WriteLine("The specified blob does not exist.");
                    return ResizeResult.Error;
                }
            }
            catch (StorageException ex)
            {
                Console.WriteLine("The specified storage account credentials are invalid. " + ex.ToString());
                return ResizeResult.Error;
            }

            // Determine blob attributes
            Console.WriteLine("[{0}] Determining blob size...", DateTime.Now.ToShortTimeString());
            blob.FetchAttributes();
            originalLength = blob.Properties.Length;

            // Read current footer
            Console.WriteLine("[{0}] Reading VHD file format footer...", DateTime.Now.ToShortTimeString());
            footer = new byte[512];
            using (var stream = new MemoryStream())
            {
                blob.DownloadRangeToStream(stream, originalLength - 512, 512);
                stream.Position = 0;
                stream.Read(footer, 0, 512);
                stream.Close();
            }

            footerInstance = Footer.FromBytes(footer, 0);

            // Make sure this is a "fixed" disk
            if (footerInstance.DiskType != FileType.Fixed)
            {
                Console.WriteLine("The specified VHD blob is not a fixed-size disk. WindowsAzureDiskResizer can only resize fixed-size VHD files.");
                return ResizeResult.Error;
            }
            if (footerInstance.CurrentSize >= (long)NewSize.Bytes)
            {
                // The specified VHD blob is larger than the specified new size. Shrinking disks is a potentially dangerous operation
                // Ask the user for confirmation
                return ResizeResult.Shrink;
            }
            return DoResizeVhdBlob();
        }

        public ResizeResult DoResizeVhdBlob()
        {
            Console.WriteLine("[{0}] VHD file format fixed, current size {1} bytes.", DateTime.Now.ToShortTimeString(), footerInstance.CurrentSize);

            // Expand the blob
            Console.WriteLine("[{0}] Resizing containing blob...", DateTime.Now.ToShortTimeString());
            blob.Resize((long)NewSize.Bytes + 512L);

            // Change footer size values
            Console.WriteLine("[{0}] Updating VHD file format footer...", DateTime.Now.ToShortTimeString());
            footerInstance.CurrentSize = (long)NewSize.Bytes;
            footerInstance.OriginalSize = (long)NewSize.Bytes;
            footerInstance.Geometry = Geometry.FromCapacity((long)NewSize.Bytes);
            footerInstance.UpdateChecksum();

            footer = new byte[512];
            footerInstance.ToBytes(footer, 0);

            Console.WriteLine("[{0}] New VHD file size {1} bytes, checksum {2}.", DateTime.Now.ToShortTimeString(), footerInstance.CurrentSize, footerInstance.Checksum);

            // Write new footer
            Console.WriteLine("[{0}] Writing VHD file format footer...", DateTime.Now.ToShortTimeString());
            using (var stream = new MemoryStream(footer))
            {
                blob.WritePages(stream, (long)NewSize.Bytes);
            }

            // Write 0 values where the footer used to be
            if (IsExpand)
            {
                Console.WriteLine("[{0}] Overwriting the old VHD file footer with zeroes...", DateTime.Now.ToShortTimeString());
                blob.ClearPages(originalLength - 512, 512);
            }

            // Done!
            Console.WriteLine("[{0}] Done!", DateTime.Now.ToShortTimeString());
            return ResizeResult.Success;
        }
    }
}
