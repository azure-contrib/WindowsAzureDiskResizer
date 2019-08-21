# WindowsAzureDiskResizer

Resizes a Microsoft Azure virtual disk directly in blob storage.

See http://blog.maartenballiauw.be/post/2013/01/07/Tales-from-the-trenches-resizing-a-Windows-Azure-virtual-disk-the-smooth-way.aspx for more info.

Binaries: http://blog.maartenballiauw.be/file.axd?file=2013%2f1%2fWindowsAzureDiskResizer-1.0.0.0.zip

## Growing disks

The following steps should be taken for growing a disk:
* Shutdown the VM
* Delete the VM -or- detach the disk if it’s not the OS disk
* In the Microsoft Azure portal, delete the disk (retain the data!) do that the lease Microsoft Azure has on it is removed
* Run WindowsAzureDiskResizer with the correct parameters
* In the Microsoft Azure portal, recreate the disk based on the existing blob
* Recreate the VM  -or- reattach the disk if it’s not the OS disk
* Start the VM
* Use diskpart / disk management to resize the partition

## Shrinking disks

Note that shrinking a disk is a potentially dangerous operation and is currently unsupported by the tool. If this is required, the following *may* work, use at your own risk.

* Modify Program.cs and remove the "if (footerInstance.CurrentSize >= newSize)" check

* Use diskpart / disk management to resize the partition to a size smaller than the current
  * For Windows, check https://technet.microsoft.com/en-us/magazine/gg309169.aspx on how to do this.
  * Some resources for Linux:
    * http://askubuntu.com/questions/390769/how-do-i-resize-partitions-using-command-line-without-using-a-gui-on-a-server
    * https://www.howtoforge.com/linux_resizing_ext3_partitions
    * http://positon.org/resize-an-ext3-ext4-partition
    * http://forums.justlinux.com/showthread.php?147711-Resizing-Ext2-Ext3-Partitions-resize2fs-and-fdisk
* Shutdown the VM
* Delete the VM -or- detach the disk if it’s not the OS disk
* In the Microsoft Azure portal, delete the disk (retain the data!) do that the lease Microsoft Azure has on it is removed
* Run WindowsAzureDiskResizer with the correct parameters
* In the Microsoft Azure portal, recreate the disk based on the existing blob
* Recreate the VM  -or- reattach the disk if it’s not the OS disk
* Start the VM

## Disclaimer
Even though I have tested this on a couple of data disks without any problems, you are using the provided code and/or binaries at your own risk! I’m not responsible if something breaks! The provided code is as-is without warranty!
