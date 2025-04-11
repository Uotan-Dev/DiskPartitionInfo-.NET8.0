using System;
using System.IO;
using DiskPartitionInfo.Gpt;

namespace DiskPartitionInfo.FluentApi
{
    public interface IGptWriter
    {
        /// <summary>
        /// 将GPT表信息写入到指定路径的文件中。
        /// </summary>
        /// <param name="gpt">要写入的GPT表信息</param>
        /// <param name="path">文件保存路径</param>
        /// <param name="format">输出格式，默认为"json"</param>
        /// <returns>成功写入返回true，否则返回false</returns>
        bool ToFile(GuidPartitionTable gpt, string path, string format = "json");

        /// <summary>
        /// 将GPT表信息写入到指定的流中。
        /// </summary>
        /// <param name="gpt">要写入的GPT表信息</param>
        /// <param name="stream">目标流</param>
        /// <param name="format">输出格式，默认为"json"</param>
        /// <returns>成功写入返回true，否则返回false</returns>
        bool ToStream(GuidPartitionTable gpt, Stream stream, string format = "json");
        
        /// <summary>
        /// 将GPT表信息以二进制磁盘格式写入到指定路径的文件中。
        /// </summary>
        /// <param name="gpt">要写入的GPT表信息</param>
        /// <param name="path">文件保存路径</param>
        /// <param name="includeMbr">是否包含保护性MBR，默认为true</param>
        /// <param name="sectorSize">扇区大小，默认为512字节</param>
        /// <returns>成功写入返回true，否则返回false</returns>
        bool ToBinaryFile(GuidPartitionTable gpt, string path, bool includeMbr = true, int sectorSize = 512);
        
        /// <summary>
        /// 将GPT表信息以二进制磁盘格式写入到指定的流中。
        /// </summary>
        /// <param name="gpt">要写入的GPT表信息</param>
        /// <param name="stream">目标流</param>
        /// <param name="includeMbr">是否包含保护性MBR，默认为true</param>
        /// <param name="sectorSize">扇区大小，默认为512字节</param>
        /// <returns>成功写入返回true，否则返回false</returns>
        bool ToBinaryStream(GuidPartitionTable gpt, Stream stream, bool includeMbr = true, int sectorSize = 512);
    }
}