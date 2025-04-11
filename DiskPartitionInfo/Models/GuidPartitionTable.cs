using System;
using System.Runtime.InteropServices;

namespace DiskPartitionInfo.Models
{
    [StructLayout(LayoutKind.Sequential, Size = 512, Pack = 1)]
    internal struct GuidPartitionTable
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public char[] Signature;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Revision;

        public uint HeaderSize;

        public uint HeaderCrc32;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Reserved;

        public ulong PrimaryHeaderLocation;

        public ulong SecondaryHeaderLocation;

        public ulong FirstUsableLba;

        public ulong LastUsableLba;

        public Guid DiskGuid;

        public ulong PartitionsArrayLba;

        public uint PartitionsCount;

        public uint PartitionEntryLength;

        public uint PartitionsArrayCrc32;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 420)]
        public byte[] Reserved2;
    }
}
