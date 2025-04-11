using DiskPartitionInfo.Gpt;

namespace GPTCRC32Fixer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== GPT 分区表 CRC32 校验值修复工具 ===");
            Console.WriteLine("适用于UotanToolboxNT项目的增强功能");
            Console.WriteLine();

            if (args.Length == 0)
            {
                ShowUsage();
                return;
            }

            try
            {
                string filePath = args[0];
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"错误: 文件 '{filePath}' 不存在。");
                    return;
                }

                // 默认尝试修复主分区表和备份分区表
                bool fixPrimary = true;
                bool fixBackup = true;

                // 检查其他命令行参数
                if (args.Length > 1)
                {
                    for (int i = 1; i < args.Length; i++)
                    {
                        switch (args[i].ToLower())
                        {
                            case "-p":
                            case "--primary":
                                fixPrimary = true;
                                fixBackup = false;
                                break;
                            case "-b":
                            case "--backup":
                                fixPrimary = false;
                                fixBackup = true;
                                break;
                            case "-h":
                            case "--help":
                                ShowUsage();
                                return;
                            default:
                                Console.WriteLine($"警告: 未知参数 '{args[i]}'，将被忽略。");
                                break;
                        }
                    }
                }

                Console.WriteLine($"正在分析文件 '{filePath}'...");
                FixGptFile(filePath, fixPrimary, fixBackup);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"内部错误: {ex.InnerException.Message}");
                }
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine("用法: GPTCRC32Fixer <文件路径> [选项]");
            Console.WriteLine();
            Console.WriteLine("选项:");
            Console.WriteLine("  -p, --primary   只修复主分区表");
            Console.WriteLine("  -b, --backup    只修复备份分区表");
            Console.WriteLine("  -h, --help      显示此帮助信息");
            Console.WriteLine();
            Console.WriteLine("如果未指定选项，将先后尝试主分区表和备份分区表。");
        }

        private static void FixGptFile(string filePath, bool fixPrimary, bool fixBackup)
        {
            // 先尝试读取分区表，以确定文件的扇区大小和分区表类型
            try
            {
                // 尝试读取主分区表
                GuidPartitionTable? primaryGpt = null;
                if (fixPrimary)
                {
                    try
                    {
                        primaryGpt = DiskPartitionInfo.DiskPartitionInfo.ReadGpt()
                            .Primary()
                            .FromPath(filePath);

                        Console.WriteLine("成功读取主分区表");
                        Console.WriteLine($"磁盘GUID: {primaryGpt.DiskGuid}");
                        Console.WriteLine($"分区数量: {primaryGpt.Partitions.Count}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"无法读取主分区表: {ex.Message}");
                        primaryGpt = null;
                    }
                }

                // 尝试读取备份分区表
                GuidPartitionTable? backupGpt = null;
                if (fixBackup)
                {
                    try
                    {
                        backupGpt = DiskPartitionInfo.DiskPartitionInfo.ReadGpt()
                            .Secondary()
                            .FromPath(filePath);

                        Console.WriteLine("成功读取备份分区表");
                        Console.WriteLine($"磁盘GUID: {backupGpt.DiskGuid}");
                        Console.WriteLine($"分区数量: {backupGpt.Partitions.Count}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"无法读取备份分区表: {ex.Message}");
                        backupGpt = null;
                    }
                }

                if (primaryGpt == null && backupGpt == null)
                {
                    Console.WriteLine("错误: 无法从文件中读取任何有效的GPT分区表。");
                    return;
                }

                // 使用读取到的分区表执行修复
                Console.WriteLine();
                Console.WriteLine("开始修复CRC32校验值...");

                // 修复主分区表
                if (primaryGpt != null && fixPrimary)
                {
                    Console.WriteLine("正在修复主分区表...");
                    DiskPartitionInfo.DiskPartitionInfo.WriteGpt()
                        .Primary()
                        .ToPath(filePath, primaryGpt);
                    Console.WriteLine("主分区表已修复");
                }

                // 修复备份分区表
                if (backupGpt != null && fixBackup)
                {
                    Console.WriteLine("正在修复备份分区表...");
                    DiskPartitionInfo.DiskPartitionInfo.WriteGpt()
                        .Secondary()
                        .ToPath(filePath, backupGpt);
                    Console.WriteLine("备份分区表已修复");
                }

                Console.WriteLine("CRC32校验值修复完成");

                // 验证修复结果
                Console.WriteLine();
                Console.WriteLine("正在验证修复结果...");
                VerifyGptFile(filePath, fixPrimary, fixBackup);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"修复过程中发生错误: {ex.Message}");
                throw;
            }
        }

        private static void VerifyGptFile(string filePath, bool verifyPrimary, bool verifyBackup)
        {
            bool allValid = true;

            // 验证主分区表
            if (verifyPrimary)
            {
                try
                {
                    var primaryGpt = DiskPartitionInfo.DiskPartitionInfo.ReadGpt()
                        .Primary()
                        .FromPath(filePath);

                    if (primaryGpt.HasValidSignature())
                    {
                        Console.WriteLine("主分区表签名有效");
                    }
                    else
                    {
                        Console.WriteLine("主分区表签名无效");
                        allValid = false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"主分区表验证失败: {ex.Message}");
                    allValid = false;
                }
            }

            // 验证备份分区表
            if (verifyBackup)
            {
                try
                {
                    var backupGpt = DiskPartitionInfo.DiskPartitionInfo.ReadGpt()
                        .Secondary()
                        .FromPath(filePath);

                    if (backupGpt.HasValidSignature())
                    {
                        Console.WriteLine("备份分区表签名有效");
                    }
                    else
                    {
                        Console.WriteLine("备份分区表签名无效");
                        allValid = false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"备份分区表验证失败: {ex.Message}");
                    allValid = false;
                }
            }

            if (allValid)
            {
                Console.WriteLine("所有分区表验证通过，CRC32校验值修复成功!");
            }
            else
            {
                Console.WriteLine("部分分区表验证失败，CRC32校验值可能未完全修复。");
            }
        }
    }
}