using System;

namespace ConsoleApp
{
	class Program
	{
		static void Main(string[] args)
		{
			BaiduSearchSpider spider = new BaiduSearchSpider();
			spider.Run();
			Console.WriteLine("Compelte.");
		}
	}
}
