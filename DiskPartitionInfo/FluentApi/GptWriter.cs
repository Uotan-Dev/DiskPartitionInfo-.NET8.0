using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DiskPartitionInfo.Gpt;

namespace DiskPartitionInfo.FluentApi
{
    internal class GptWriter : IGptWriter
    {
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