using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace FreqSpider
{
    internal class Spider
    {
        //字词列表的文件路径
        private readonly string _filePath;
        //写入文件的路径
        private readonly string _freqPath;
        //并发数
        private readonly int _concurrency;
        //原始字词列表
        private readonly HashSet<string> _words;
        //有词频的的字词列表
        private readonly ConcurrentDictionary<string, int> _words_freqs;
        //Http客户端
        private readonly HttpClient _httpClient;
        //同步锁
        private readonly object _syncLock = new();

        //构造函数
        public Spider(string filePath, int concurrency, int timeout)
        {
            _filePath = filePath;
            string directory = Path.GetDirectoryName(_filePath) ?? ".";
            _freqPath = Path.Combine(directory, "freq.txt");
            _concurrency = concurrency;
            _words = new HashSet<string>(0);
            _words_freqs = new ConcurrentDictionary<string, int>(concurrency, 0);
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://bcc.blcu.edu.cn/zh/search/0/"),
                Timeout = TimeSpan.FromSeconds(timeout)
            };
        }

        //载入字词列表
        private bool LoadWords()
        {
            try
            {
                using StreamReader sr = new(_filePath, System.Text.Encoding.UTF8);
                string? line;
                while ((line = sr.ReadLine()) != null)
                    _words.Add(line);//HashSet不会有重复项
                if (_words.Count == 0)
                    throw new Exception("文件中不含有效的字词！");
                Console.WriteLine($"读取成功！共需爬取{_words.Count}个字词。");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"读取错误：\n{e.Message}");
                return false;
            }
        }

        //获取页面
        private async Task<string> GetPageof(string word)
        {
            var response = await _httpClient.GetAsync(word);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        //从页面中提取某个字词的词频
        private static int ExtractFreq(string word, string page)
        {
            string wordPattern = @"input"" value=""" + word;
            Match wordMatch = Regex.Match(page, wordPattern);
            if (!wordMatch.Success)
                throw new Exception($"该词中有网站不支持的字符！");

            string freqPattern = @"totalnum"" value=""\d+";
            Match freqMatch = Regex.Match(page, freqPattern);
            if (!freqMatch.Success)
                throw new Exception("网站中没有该词的词频！");
            int freq = int.Parse(freqMatch.ValueSpan[17..]);

            Console.WriteLine($"“{word}”的词频：{freq}");
            return freq;
        }

        //获取词频
        private async Task<int> GetFreqof(string word)
        {
            try
            {
                string page = await GetPageof(word);
                return ExtractFreq(word, page);
            }
            catch (Exception e)
            {
                Console.WriteLine($"错误：已令“{word}”的词频为-1，因为{e.Message}");
                return -1;
            }
        }

        //同步写入文件
        private void AppendWrite(string word, int freq)
        {
            lock (_syncLock)
            {
                using StreamWriter sw = new(_freqPath, true, System.Text.Encoding.UTF8);
                sw.WriteLine($"{word}\t{freq}");
            }
        }

        //逐个爬取词频，每爬到一个就写入一个，防止中途崩溃
        private async Task Crawl()
        {
            try
            {
                using var semaphore = new SemaphoreSlim(_concurrency);
                var tasks = _words.Select(async word =>
                {
                    await semaphore.WaitAsync();
                    int freq = await GetFreqof(word);
                    _words_freqs.TryAdd(word, freq);
                    try { AppendWrite(word, freq); }
                    finally { semaphore.Release(); }
                });
                await Task.WhenAll(tasks);
                Console.WriteLine($"爬取完成！共爬取{_words_freqs.Count}个字词。");
            }
            catch (Exception e)
            {
                Console.WriteLine($"统计错误：{e.Message}已中止。");
            }
        }

        //排序并覆写保存
        private void SortandOverride()
        {
            var sortedWords = _words_freqs.OrderByDescending(x => x.Value);
            try
            {
                using StreamWriter sw = new(_freqPath, false, System.Text.Encoding.UTF8);
                foreach (var (word, freq) in sortedWords)
                    sw.WriteLine($"{word}\t{freq}");
                Console.WriteLine($"重排序完成！统计结束。");
            }
            catch (Exception e)
            {
                Console.WriteLine($"保存错误：\n{e.Message}");
            }
        }

        //运行爬虫
        public async Task Run()
        {
            if (!LoadWords()) return;
            await Crawl();
            SortandOverride();
        }
    }
}
