using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using DiskPartitionInfo.Extensions;
using DiskPartitionInfo.Gpt;
using DiskPartitionInfo.Models;
using GptModelStruct = DiskPartitionInfo.Models.GuidPartitionTable;
using GptPartitionEntryStruct = DiskPartitionInfo.Models.GptPartitionEntry;
using GuidPartitionTable = DiskPartitionInfo.Gpt.GuidPartitionTable;

namespace DiskPartitionInfo.FluentApi
{
    internal class GptWriter : IGptWriter
    {
        private const int StandardSectorSize = 512;
        
        /// <inheritdoc/>
        public bool ToFile(GuidPartitionTable gpt, string path, string format = "json")
        {
            try
            {
                using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
                return ToStream(gpt, fileStream, format);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public bool ToStream(GuidPartitionTable gpt, Stream stream, string format = "json")
        {
            if (gpt == null)
                throw new ArgumentNullException(nameof(gpt));

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            try
            {
                switch (format.ToLowerInvariant())
                {
                    case "json":
                        return WriteAsJson(gpt, stream);
                    case "csv":
                        return WriteAsCsv(gpt, stream);
                    case "txt":
                    case "text":
                        return WriteAsText(gpt, stream);
                    default:
                        throw new ArgumentException($"不支持的格式: {format}", nameof(format));
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <inheritdoc/>
        public bool ToBinaryFile(GuidPartitionTable gpt, string path, bool includeMbr = true, int sectorSize = StandardSectorSize)
        {
            try
            {
                using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
                return ToBinaryStream(gpt, fileStream, includeMbr, sectorSize);
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <inheritdoc/>
        public bool ToBinaryStream(GuidPartitionTable gpt, Stream stream, bool includeMbr = true, int sectorSize = StandardSectorSize)
        {
            if (gpt == null)
                throw new ArgumentNullException(nameof(gpt));

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
                
            if (sectorSize < StandardSectorSize || (sectorSize % StandardSectorSize) != 0)
                throw new ArgumentException($"扇区大小必须是{StandardSectorSize}的倍数", nameof(sectorSize));
                
            try
            {
                // 写入保护性MBR（如果需要）
                if (includeMbr)
                {
                    WriteProtectiveMbr(stream, sectorSize);
                }
                
                // 写入GPT头
                WriteGptHeader(stream, gpt, sectorSize);
                
                // 写入GPT分区表
                WriteGptPartitionEntries(stream, gpt, sectorSize);
                
                // 如果需要，可以在这里写入占位数据（从最后一个分区表条目到FirstUsableLba）
                
                // 写入备份GPT分区表和头（可选，这里简化处理）
                // 先计算备份GPT分区表的位置
                ulong backupPartitionArrayLba = gpt.SecondaryHeaderLocation - ((ulong)gpt.Partitions.Count * 128 / (ulong)sectorSize);
                
                // 写入备份分区表
                stream.Seek((long)backupPartitionArrayLba * sectorSize, SeekOrigin.Begin);
                WriteGptPartitionEntries(stream, gpt, sectorSize);
                
                // 写入备份GPT头
                stream.Seek((long)gpt.SecondaryHeaderLocation * sectorSize, SeekOrigin.Begin);
                WriteGptHeader(stream, gpt, sectorSize, isBackup: true);
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void WriteProtectiveMbr(Stream stream, int sectorSize)
        {
            // 创建保护性MBR
            var mbr = new ClassicalMasterBootRecord
            {
                BootstrapCodeArea = new byte[446],
                PartitionEntries = new MbrPartitionEntry[4],
                BootSignature = new byte[] { 0x55, 0xAA } // 有效的MBR签名
            };
            
            // 初始化引导代码区域（通常为零）
            for (int i = 0; i < mbr.BootstrapCodeArea.Length; i++)
            {
                mbr.BootstrapCodeArea[i] = 0;
            }
            
            // 设置保护性分区项
            mbr.PartitionEntries[0] = new MbrPartitionEntry
            {
                Status = 0x00, // 非活动分区
                FirstBootableSector = new byte[3] { 0, 0, 0 },
                PartitionType = 0xEE, // GPT保护性分区类型
                LastBootableSector = new byte[3] { 0xFF, 0xFF, 0xFF }, // 最大可能值
                FirstAbsoluteSector = 1, // MBR后的第一个扇区
                SectorsCount = 0xFFFFFFFF // 最大可能值（表示整个磁盘）
            };
            
            // 初始化其他分区项为空
            for (int i = 1; i < 4; i++)
            {
                mbr.PartitionEntries[i] = new MbrPartitionEntry
                {
                    Status = 0,
                    FirstBootableSector = new byte[3],
                    PartitionType = 0, // 未使用
                    LastBootableSector = new byte[3],
                    FirstAbsoluteSector = 0,
                    SectorsCount = 0
                };
            }
            
            // 将MBR结构转换为字节数组
            byte[] mbrBytes = StructToBytes(mbr);
            
            // 写入MBR到流的开始位置
            stream.Position = 0;
            stream.Write(mbrBytes, 0, mbrBytes.Length);
            
            // 如果扇区大小大于MBR大小，填充剩余空间
            if (sectorSize > mbrBytes.Length)
            {
                byte[] padding = new byte[sectorSize - mbrBytes.Length];
                stream.Write(padding, 0, padding.Length);
            }
        }
        
        private void WriteGptHeader(Stream stream, GuidPartitionTable gpt, int sectorSize, bool isBackup = false)
        {
            // 获取原始GPT头结构
            var gptHeader = GetGptHeader(gpt);
            
            // 如果是备份GPT头，交换主备位置
            if (isBackup)
            {
                (gptHeader.PrimaryHeaderLocation, gptHeader.SecondaryHeaderLocation) = 
                    (gptHeader.SecondaryHeaderLocation, gptHeader.PrimaryHeaderLocation);
                    
                // 更新CRC32（实际应用中应重新计算）
                // 这里为简化处理，没有实际计算CRC32
            }
            
            // 将GPT头结构转换为字节数组
            byte[] headerBytes = StructToBytes(gptHeader);
            
            // 写入GPT头到流
            stream.Write(headerBytes, 0, headerBytes.Length);
            
            // 如果扇区大小大于GPT头大小，填充剩余空间
            if (sectorSize > headerBytes.Length)
            {
                byte[] padding = new byte[sectorSize - headerBytes.Length];
                stream.Write(padding, 0, padding.Length);
            }
        }
        
        private void WriteGptPartitionEntries(Stream stream, GuidPartitionTable gpt, int sectorSize)
        {
            // 获取所有分区项
            var partitionEntries = GetGptPartitionEntries(gpt);
            
            // 计算分区表总大小
            int partitionTableSize = partitionEntries.Length * Marshal.SizeOf<GptPartitionEntryStruct>();
            
            // 计算需要多少个扇区来存储分区表
            int sectorsRequired = (partitionTableSize + sectorSize - 1) / sectorSize;
            
            // 创建足够大的缓冲区来存储所有分区项
            byte[] buffer = new byte[sectorsRequired * sectorSize];
            
            // 将每个分区项写入缓冲区
            int offset = 0;
            foreach (var entry in partitionEntries)
            {
                byte[] entryBytes = StructToBytes(entry);
                Buffer.BlockCopy(entryBytes, 0, buffer, offset, entryBytes.Length);
                offset += entryBytes.Length;
            }
            
            // 写入分区表到流
            stream.Write(buffer, 0, buffer.Length);
        }
        
        private GptModelStruct GetGptHeader(GuidPartitionTable gpt)
        {
            // 创建一个GPT头结构，基于提供的GuidPartitionTable对象
            var header = new GptModelStruct
            {
                Signature = "EFI PART".ToCharArray(),
                Revision = new byte[] { 0, 0, 1, 0 }, // 版本1.0
                HeaderSize = 92, // GPT头的标准大小
                HeaderCrc32 = 0, // 应该在填充所有字段后重新计算
                Reserved = new byte[4], // 保留字段设为零
                PrimaryHeaderLocation = gpt.PrimaryHeaderLocation,
                SecondaryHeaderLocation = gpt.SecondaryHeaderLocation,
                FirstUsableLba = gpt.FirstUsableLba,
                LastUsableLba = gpt.LastUsableLba,
                DiskGuid = gpt.DiskGuid,
                PartitionsArrayLba = 2, // 通常GPT分区表位于LBA 2
                PartitionsCount = (uint)gpt.Partitions.Count,
                PartitionEntryLength = 128, // GPT分区项的标准大小
                PartitionsArrayCrc32 = 0, // 应该在填充所有分区项后重新计算
                Reserved2 = new byte[420] // 剩余空间设为零
            };
            
            // 填充保留字段为零
            for (int i = 0; i < header.Reserved.Length; i++)
                header.Reserved[i] = 0;
                
            for (int i = 0; i < header.Reserved2.Length; i++)
                header.Reserved2[i] = 0;
                
            // 注意：实际应用中，应该计算CRC32校验和
            
            return header;
        }
        
        private GptPartitionEntryStruct[] GetGptPartitionEntries(GuidPartitionTable gpt)
        {
            // 创建分区项数组
            var entries = new GptPartitionEntryStruct[gpt.Partitions.Count];
            
            // 填充分区项
            int index = 0;
            foreach (var partition in gpt.Partitions)
            {
                var entry = new GptPartitionEntryStruct
                {
                    PartitionType = partition.Type,
                    PartitionGuid = partition.Guid,
                    FirstLba = partition.FirstLba,
                    LastLba = partition.LastLba,
                    AttributeFlags = new byte[8],
                    Name = partition.Name ?? string.Empty
                };
                
                // 设置属性标志
                if (partition.IsRequired)
                    entry.AttributeFlags[0] |= 0x01;
                    
                if (partition.IsReadOnly)
                    entry.AttributeFlags[7] |= 0x10;
                    
                if (partition.IsHidden)
                    entry.AttributeFlags[7] |= 0x40;
                    
                if (partition.IsShadowCopy)
                    entry.AttributeFlags[7] |= 0x20;
                    
                if (partition.ShouldNotHaveDriveLetterAssigned)
                    entry.AttributeFlags[7] |= 0x80;
                    
                entries[index++] = entry;
            }
            
            return entries;
        }

        private byte[] StructToBytes<T>(T structure) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] buffer = new byte[size];
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            
            try
            {
                Marshal.StructureToPtr(structure, handle.AddrOfPinnedObject(), false);
                return buffer;
            }
            finally
            {
                handle.Free();
            }
        }

        private bool WriteAsJson(GuidPartitionTable gpt, Stream stream)
        {
            // 创建一个匿名对象来表示GPT的可序列化视图
            var gptData = new
            {
                HasValidSignature = gpt.HasValidSignature(),
                DiskGuid = gpt.DiskGuid.ToString(),
                PrimaryHeaderLocation = gpt.PrimaryHeaderLocation,
                SecondaryHeaderLocation = gpt.SecondaryHeaderLocation,
                FirstUsableLba = gpt.FirstUsableLba,
                LastUsableLba = gpt.LastUsableLba,
                Partitions = gpt.Partitions != null ? Array.ConvertAll(gpt.Partitions.ToArray(), p => new
                {
                    Name = p.Name,
                    Type = p.Type.ToString(),
                    Guid = p.Guid.ToString(),
                    FirstLba = p.FirstLba,
                    LastLba = p.LastLba,
                    IsRequired = p.IsRequired,
                    IsReadOnly = p.IsReadOnly,
                    IsHidden = p.IsHidden,
                    IsShadowCopy = p.IsShadowCopy,
                    ShouldNotHaveDriveLetterAssigned = p.ShouldNotHaveDriveLetterAssigned
                }) : Array.Empty<object>()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(gptData, options);
            stream.Write(jsonBytes, 0, jsonBytes.Length);
            return true;
        }

        private bool WriteAsCsv(GuidPartitionTable gpt, Stream stream)
        {
            using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
            
            // 写入CSV表头
            writer.WriteLine("Name,Type,Guid,FirstLba,LastLba,IsRequired,IsReadOnly,IsHidden,IsShadowCopy,ShouldNotHaveDriveLetterAssigned");
            
            // 写入分区信息
            if (gpt.Partitions != null)
            {
                foreach (var partition in gpt.Partitions)
                {
                    if (partition.Type != Guid.Empty) // 只输出有效分区
                    {
                        writer.WriteLine(
                            $"\"{partition.Name}\",{partition.Type},{partition.Guid},{partition.FirstLba}," +
                            $"{partition.LastLba},{partition.IsRequired},{partition.IsReadOnly}," +
                            $"{partition.IsHidden},{partition.IsShadowCopy},{partition.ShouldNotHaveDriveLetterAssigned}");
                    }
                }
            }
            
            writer.Flush();
            return true;
        }

        private bool WriteAsText(GuidPartitionTable gpt, Stream stream)
        {
            using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
            
            writer.WriteLine("GPT 分区表信息:");
            writer.WriteLine("------------------------");
            writer.WriteLine($"签名有效: {gpt.HasValidSignature()}");
            writer.WriteLine($"磁盘GUID: {gpt.DiskGuid}");
            writer.WriteLine($"主GPT表位置: {gpt.PrimaryHeaderLocation}");
            writer.WriteLine($"备份GPT表位置: {gpt.SecondaryHeaderLocation}");
            writer.WriteLine($"第一个可用LBA: {gpt.FirstUsableLba}");
            writer.WriteLine($"最后一个可用LBA: {gpt.LastUsableLba}");
            writer.WriteLine("------------------------");
            writer.WriteLine("分区信息:");
            writer.WriteLine("------------------------");
            
            if (gpt.Partitions != null)
            {
                int index = 0;
                foreach (var partition in gpt.Partitions)
                {
                    if (partition.Type != Guid.Empty) // 只输出有效分区
                    {
                        writer.WriteLine($"分区 #{++index}");
                        writer.WriteLine($"  名称: {partition.Name}");
                        writer.WriteLine($"  类型: {partition.Type}");
                        writer.WriteLine($"  GUID: {partition.Guid}");
                        writer.WriteLine($"  起始LBA: {partition.FirstLba}");
                        writer.WriteLine($"  结束LBA: {partition.LastLba}");
                        writer.WriteLine($"  是必需的: {partition.IsRequired}");
                        writer.WriteLine($"  是只读的: {partition.IsReadOnly}");
                        writer.WriteLine($"  是隐藏的: {partition.IsHidden}");
                        writer.WriteLine($"  是卷影副本: {partition.IsShadowCopy}");
                        writer.WriteLine($"  不应分配驱动器号: {partition.ShouldNotHaveDriveLetterAssigned}");
                        writer.WriteLine("------------------------");
                    }
                }
            }
            
            writer.Flush();
            return true;
        }
    }
}