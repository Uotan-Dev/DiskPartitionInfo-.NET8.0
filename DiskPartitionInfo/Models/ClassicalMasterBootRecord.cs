using System.Runtime.InteropServices;

namespace DiskPartitionInfo.Models
{
    [StructLayout(LayoutKind.Sequential, Size = 512, Pack = 1)]
    internal struct ClassicalMasterBootRecord
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 446)]
        public byte[] BootstrapCodeArea;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public MbrPartitionEntry[] PartitionEntries;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] BootSignature;
    }
}
