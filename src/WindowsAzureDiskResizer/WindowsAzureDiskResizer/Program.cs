using System;
using WindowsAzureDiskResizer.Helpers;

namespace WindowsAzureDiskResizer
{
    internal class Program
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
            if (!long.TryParse(args[0], out long newSizeInGb) || (newSizeInGb * 1024 * 1024 * 1024) % 512 != 0)
            {
                Console.WriteLine("Argument size invalid. Please specify a valid disk size in GB (must be a whole number).");
                return -1;
            }
            if (!Uri.TryCreate(args[1], UriKind.Absolute, out Uri blobUri))
            {
                Console.WriteLine("Argument bloburl invalid. Please specify a valid URL with an HTTP or HTTP schema.");
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
                Console.WriteLine("Please specify either a blob URL with a shared access signature that allows write access or provide full storage credentials.");
                return -1;
            }

            // Verify size. Size for disk must be <= 1023 GB
            if (newSizeInGb > 1023)
            {
                Console.WriteLine("The given disk size exceeds 1023 GB. Windows Azure will not be able to start the virtual machine stored on this disk if you continue.");
                Console.WriteLine("See https://msdn.microsoft.com/en-us/library/azure/dn197896.aspx for more information.");
                return -1;
            }

            // Start the resize process
            var resizeVhdHelper = new ResizeVhdHelper();
            var result = resizeVhdHelper.ResizeVhdBlob((int)newSizeInGb, blobUri, accountName, accountKey);
            if (result != ResizeResult.Shrink)
                return (int)result;

            Console.WriteLine("The specified VHD blob is larger than the specified new size. Shrinking disks is a potentially dangerous operation.");
            Console.WriteLine("Do you want to continue with shrinking the disk? (y/n)");
            while (true)
            {
                var consoleKey = Console.ReadKey().KeyChar;
                if (consoleKey == 'n')
                {
                    Console.WriteLine("Aborted.");
                    return -1;
                }
                if (consoleKey == 'y')
                {
                    resizeVhdHelper.IsExpand = false;
                    var finalResult = resizeVhdHelper.DoResizeVhdBlob();
                    return (int)finalResult;
                }
            }
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
