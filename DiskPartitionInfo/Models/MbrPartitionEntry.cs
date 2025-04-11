using System.Runtime.InteropServices;

namespace DiskPartitionInfo.Models
{
    [StructLayout(LayoutKind.Sequential, Size = 16, Pack = 1)]
    internal struct MbrPartitionEntry
    {
        public byte Status;

        /// <summary>
        /// CHS address
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] FirstBootableSector;

        public byte PartitionType;

        /// <summary>
        /// CHS address
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] LastBootableSector;

        public uint FirstAbsoluteSector;

        public uint SectorsCount;
    }
}
