using System.IO;
using DiskPartitionInfo.Mbr;

namespace DiskPartitionInfo.FluentApi
{
    public interface IMbrReader
    {
        /// <summary>
        /// Reads the MBR from the given path.
        /// It can be a path to a file or to a physical drive.
        /// </summary>
        /// <param name="path">Path to disk or file to read from,
        /// e.g. C:\MBR.bin or ../disk.img.</param>
        /// <returns>The Master Boot Record information.</returns>
        MasterBootRecord FromPath(string path);

        /// <summary>
        /// Reads the MBR from the given stream.
        /// The stream is not automatically closed after read operation.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <returns>The Master Boot Record information.</returns>
        MasterBootRecord FromStream(Stream stream);
    }
}
