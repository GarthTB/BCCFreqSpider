using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace FreqSpider
{
    internal class Spider
    {
        //字词列表的文件路径
        private readonly string _filePath;
        //并发数
        private readonly int _concurrency;
        //原始字词列表
        private readonly HashSet<string> _words;
        //有词频的的字词列表
        private readonly ConcurrentDictionary<string, int> _words_freqs;
        //Http客户端
        private readonly HttpClient _httpClient;

        //构造函数
        public Spider(string filePath, int concurrency, int timeout)
        {
            _filePath = filePath;
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
                    if (!_words.Contains(line))
                        _words.Add(line);
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

        //从页面中获取某个字词的词频
        private async Task<int> GetFreqof(string word)
        {
            try
            {
                var response = await _httpClient.GetAsync(word);
                response.EnsureSuccessStatusCode();
                string page = await response.Content.ReadAsStringAsync();
                string pattern = @"totalnum"" value=""\d+";
                Match match = Regex.Match(page, pattern);
                if (!match.Success)
                    throw new Exception($"获取“{word}”的词频失败！");
                int freq = int.Parse(match.ValueSpan[17..]);
                Console.WriteLine($"“{word}”的词频为：{freq}");
                return freq;
            }
            catch (Exception e)
            {
                Console.WriteLine($"错误：{e.Message}");
                return -1;
            }
        }

        //逐个爬取词频
        private async Task Crawl()
        {
            using var semaphore = new SemaphoreSlim(_concurrency);
            var tasks = new List<Task>();
            foreach (var word in _words)
            {
                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    int freq = await GetFreqof(word);
                    _words_freqs.AddOrUpdate(word, freq, (k, v) => freq);
                    semaphore.Release();
                });
                tasks.Add(task);
            }
            await Task.WhenAll(tasks);
        }

        //保存到文件
        private void SaveFile()
        {
            var sortedWords = _words_freqs.OrderByDescending(x => x.Value);
            try
            {
                string directory = Path.GetDirectoryName(_filePath) ?? ".";
                string freqFilePath = Path.Combine(directory, "freq.txt");
                using StreamWriter sw = new(freqFilePath, false, System.Text.Encoding.UTF8);
                foreach (var (word, freq) in sortedWords)
                    sw.WriteLine($"{word}\t{freq}");
                Console.WriteLine($"保存成功！统计结束。");
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
            SaveFile();
        }
    }
}
