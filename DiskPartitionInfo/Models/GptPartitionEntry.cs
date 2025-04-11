using System;
using System.Runtime.InteropServices;

namespace DiskPartitionInfo.Models
{
    [StructLayout(LayoutKind.Sequential, Size = 128, Pack = 1, CharSet = CharSet.Unicode)]
    internal struct GptPartitionEntry
    {
        public Guid PartitionType;

        public Guid PartitionGuid;

        public ulong FirstLba;

        public ulong LastLba;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] AttributeFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 36)]
        public string Name;
    }
}
