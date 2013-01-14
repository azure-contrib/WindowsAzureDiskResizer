using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using WindowsAzureDiskResizer.DiscUtils;

namespace WindowsAzureDiskResizer
{
    class Program
    {
        private static int Main(string[] args)
        {
            WriteHeader();

            // Check argument count
            if (args.Length < 2)
            {
                WriteUsage();
                return -1;
            }

            // Parse arguments
            long newSizeInGb = 0;
            if (!long.TryParse(args[0], out newSizeInGb) || (newSizeInGb * 1024 * 1024 * 1024) % 512 != 0)
            {
                Console.WriteLine("Argument size invalid. Please specify a valid disk size in GB (must be a whole number).");
                return -1;
            }
            Uri blobUri = null;
            if (!Uri.TryCreate(args[1], UriKind.Absolute, out blobUri))
            {
                Console.WriteLine("Argument bloburl invalid. Please specify a valid URL with an http or https schema.");
                return -1;
            }
            var accountName = "";
            var accountKey = "";
            if (args.Length == 4)
            {
                accountName = args[2];
                accountKey = args[3];
            } 
            else if (!blobUri.Query.Contains("sig="))
            {
                Console.WriteLine("Please specify either a blob url with a shared access signature that allows write access or provide full storage credentials.");
                return -1;
            }

            // Verify size. Size for OS disk must be < 127 GB
            if (newSizeInGb >= 127)
            {
                Console.WriteLine("Does this disk contain an operating system? (y/n)");
                while (true)
                {
                    var consoleKey = Console.ReadKey().KeyChar;
                    if (consoleKey == 'n')
                    {
                        break;
                    }
                    if (consoleKey == 'y')
                    {
                        Console.WriteLine("The given disk size exceeds 127 GB. Windows Azure will not be able to start the virtual machine stored on this disk if you continue.");
                        Console.WriteLine("Aborted.");
                        return -1;
                    }
                }
            }

            // Start the resize process
            return ResizeVhdBlob(newSizeInGb * 1024 * 1024 * 1024, blobUri, accountName, accountKey);
        }

        private static int ResizeVhdBlob(long newSize, Uri blobUri, string accountName, string accountKey)
        {
            // Check if blob exists
            var blob = new CloudPageBlob(blobUri);
            if (!string.IsNullOrEmpty(accountName) && !string.IsNullOrEmpty(accountKey))
            {
                blob = new CloudPageBlob(blobUri, new StorageCredentials(accountName, accountKey));
            }
            try
            {
                if (!blob.Exists())
                {
                    Console.WriteLine("The specified blob does not exist.");
                    return -1;
                }
            }
            catch (StorageException ex)
            {
                Console.WriteLine("The specified storage account credentials are invalid.");
                return -1;
            }

            // Determine blob attributes
            Console.WriteLine("[{0}] Determining blob size...", DateTime.Now.ToShortTimeString());
            blob.FetchAttributes();
            var originalLength = blob.Properties.Length;

            // Read current footer
            Console.WriteLine("[{0}] Reading VHD file format footer...", DateTime.Now.ToShortTimeString());
            var footer = new byte[512];
            using (Stream stream = new MemoryStream())
            {
                blob.DownloadRangeToStream(stream, originalLength - 512, 512);
                stream.Position = 0;
                stream.Read(footer, 0, 512);
                stream.Close();
            }

            var footerInstance = Footer.FromBytes(footer, 0);

            // Make sure this is a "fixed" disk
            if (footerInstance.DiskType != FileType.Fixed)
            {
                Console.WriteLine("The specified VHD blob is not a fixed-size disk. WindowsAzureDiskResizer can only resize fixed-size VHD files.");
                return -1;
            }
            if (footerInstance.CurrentSize >= newSize)
            {
                Console.WriteLine("The specified VHD blob is larger than the specified new size. WindowsAzureDiskResizer can only expand VHD files.");
                return -1;
            }
            Console.WriteLine("[{0}] VHD file format fixed, current size {1} bytes.", DateTime.Now.ToShortTimeString(), footerInstance.CurrentSize);

            // Expand the blob
            Console.WriteLine("[{0}] Expanding containing blob...", DateTime.Now.ToShortTimeString());
            blob.Resize(newSize + 512);

            // Change footer size values
            Console.WriteLine("[{0}] Updating VHD file format footer...", DateTime.Now.ToShortTimeString());
            footerInstance.CurrentSize = newSize;
            footerInstance.OriginalSize = newSize;
            footerInstance.Geometry = Geometry.FromCapacity(newSize);

            footerInstance.UpdateChecksum();

            footer = new byte[512];
            footerInstance.ToBytes(footer, 0);

            Console.WriteLine("[{0}] New VHD file size {1} bytes, checksum {2}.", DateTime.Now.ToShortTimeString(), footerInstance.CurrentSize, footerInstance.Checksum);

            // Write new footer
            Console.WriteLine("[{0}] Writing VHD file format footer...", DateTime.Now.ToShortTimeString());
            using (Stream stream = new MemoryStream(footer))
            {
                blob.WritePages(stream, newSize);
            }

            // Write 0 values where the footer used to be
            Console.WriteLine("[{0}] Overwriting the old VHD file footer with zeroes...", DateTime.Now.ToShortTimeString());
            blob.ClearPages(originalLength - 512, 512);

            // Done!
            Console.WriteLine("[{0}] Done!", DateTime.Now.ToShortTimeString());
            return 0;
        }

        private static void WriteHeader()
        {
            Console.WriteLine("WindowsAzureDiskResizer v{0}", typeof(Program).Assembly.GetName().Version);
            Console.WriteLine("Copyright 2013 Maarten Balliauw");
            Console.WriteLine();
        }

        private static void WriteUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("   WindowsAzureDiskResizer.exe <size> <bloburl> <accountname> <accountkey>");
            Console.WriteLine();
            Console.WriteLine("     <size>         New disk size in GB");
            Console.WriteLine("     <bloburl>      Disk blob URL");
            Console.WriteLine("     <accountname>  Storage account (optional if bloburl contains SAS)");
            Console.WriteLine("     <accountkey>   Storage key (optional if bloburl contains SAS)");
            Console.WriteLine();
        }
    }
}
