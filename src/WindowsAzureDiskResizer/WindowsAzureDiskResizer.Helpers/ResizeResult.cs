namespace WindowsAzureDiskResizer.Helpers
{
    /// <summary>
    /// Represents the results the resize functionality can return.
    /// </summary>
    public enum ResizeResult : int
    {
        /// <summary>
        /// Something was wrong.
        /// </summary>
        Error = -1,

        /// <summary>
        /// The new size is less than the current size of the disk. User confirmation is needed.
        /// </summary>
        Shrink = -2,

        /// <summary>
        /// The resize operation was completed successfully.
        /// </summary>
        Success = 0
    }
}
