namespace FreqSpider
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            if (args.Length == 3) await RunWithArgs(args);
            else await RunWithoutArgs();
        }

        private static async Task RunWithArgs(string[] args)
        {
            try
            {
                if (!File.Exists(args[0]))
                    throw new FileNotFoundException("未找到参数中的文件。");

                if (!int.TryParse(args[1], out int concurrent) || concurrent < 1)
                    Console.WriteLine("并发数参数无效。已使用默认值1。");
                concurrent = 1;

                if (!int.TryParse(args[2], out int timeout) || timeout < 1)
                    Console.WriteLine("超时参数无效。已使用默认值30秒。");
                timeout = 30;

                Console.Clear();
                Console.WriteLine($"准备爬取文件：{args[0]}\n并发数：{concurrent}\n超时时间：{timeout}秒");

                Spider spider = new(args[0], concurrent, timeout);
                await spider.Run();
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("未找到参数中的文件。");
            }
            Console.WriteLine("按任意键退出。");
            Console.ReadKey();
        }

        private static async Task RunWithoutArgs()
        {
            for (; ; )
            {
                Console.WriteLine("请指定一个要爬取的字词文件，其中每行作为一个要爬取的项。");
                string filePath;
                while (!File.Exists(filePath = Console.ReadLine() ?? string.Empty))
                    Console.WriteLine("未找到文件，请重新输入。");

                Console.WriteLine("请输入并发数（留空或无效则默认为1）：");
                if (!int.TryParse(Console.ReadLine() ?? string.Empty, out int concurrent) || concurrent < 1)
                    concurrent = 1;

                Console.WriteLine("请输入超时时间（留空或无效则默认为30秒）：");
                if (!int.TryParse(Console.ReadLine() ?? string.Empty, out int timeout) || timeout < 1)
                    timeout = 30;

                Console.Clear();
                Console.WriteLine($"准备爬取文件：{filePath}\n并发数：{concurrent}\n超时时间：{timeout}秒");

                Spider spider = new(filePath, concurrent, timeout);
                await spider.Run();
                Console.WriteLine("按任意键重新开始。");
                Console.ReadKey();
            }
        }
    }
}
