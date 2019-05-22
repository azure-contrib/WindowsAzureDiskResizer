using System.IO;
using System.Linq;
using ByteSizeLib;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using DiscUtils.Vhd;

namespace WindowsAzureDiskResizer.Tests.Helpers
{
    public class VhdHelper
    {
        /// <summary>
        /// Creates local VHD files, using the parameters specified.
        /// </summary>
        /// <param name="isDynamic">True to create a dynamic VHD, False to create a fixed VHD.</param>
        /// <param name="diskSizeInGb">Size of the VHD in gigabytes.</param>
        /// <param name="filePath">Path of the VHD file.</param>
        /// <param name="diskName">Name of the volume of the VHD.</param>
        /// <param name="createTestFolderAndFile">True to create a test folder and file, otherwise they are not created.</param>
        /// <param name="testFolderName">Name of the test folder.</param>
        /// <param name="testFileName">Name of the test file, including extension.</param>
        /// <param name="testFileContents">Contents of the test file.</param>
        public static void CreateVhdDisk(bool isDynamic, int diskSizeInGb, string filePath, string diskName,
                                                bool createTestFolderAndFile = false, string testFolderName = null,
                                                string testFileName = null, string testFileContents = null)
        {
            var diskSize = (long)ByteSize.FromGigaBytes(diskSizeInGb).Bytes;

            using (var fs = new FileStream(filePath, FileMode.OpenOrCreate))
            {
                using (VirtualDisk destDisk = isDynamic ? Disk.InitializeDynamic(fs, Ownership.None, diskSize)
                                                        : Disk.InitializeFixed(fs, Ownership.None, diskSize))
                {
                    BiosPartitionTable.Initialize(destDisk, WellKnownPartitionType.WindowsNtfs);
                    var volumeManager = new VolumeManager(destDisk);

                    using (var destNtfs = NtfsFileSystem.Format(volumeManager.GetLogicalVolumes().FirstOrDefault(), diskName, new NtfsFormatOptions()))
                    {
                        destNtfs.NtfsOptions.ShortNameCreation = ShortFileNameOption.Disabled;

                        if (createTestFolderAndFile)
                        {
                            File.WriteAllText(testFileName, testFileContents);
                            destNtfs.CreateDirectory(testFolderName);

                            using (var source = new FileStream(testFileName, FileMode.Open, FileAccess.Read))
                            {
                                using (Stream dest = destNtfs.OpenFile($"{testFolderName}\\{testFileName}", FileMode.Create, FileAccess.ReadWrite))
                                {
                                    source.CopyTo(dest);
                                    dest.Flush();
                                }
                            }
                        }
                    }
                }
                //commit everything to the stream before closing
                fs.Flush();
            }
        }
    }
}
