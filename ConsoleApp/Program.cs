using System;

namespace ConsoleApp
{
	class Program
	{
		class TestClass
		{
			public int X;
		}

		struct TestStruct
		{
			public int X;
		}

		static void Main(string[] args)
		{
			TestClass c1 = new TestClass();
			TestStruct s1 = new TestStruct();
			int i1 = 5;
			string str1 = "hello world";

			c1.X = 5;
			s1.X = 5;

			TestClass c2 = c1;
			TestStruct s2 = s1;
			string str2 = str1;
			int i2 = i1;

			Console.WriteLine(ReferenceEquals(str2, str1)); //__________
			Console.WriteLine(ReferenceEquals(i1, i2));     //__________

			c1.X = 8;
			s1.X = 9;
			str1 = "welcome";
			i1 = 7;

			Console.WriteLine(c2.X);                        //__________
			Console.WriteLine(ReferenceEquals(c1, c2));     //__________
			Console.WriteLine(s2.X);                        //__________
			Console.WriteLine(ReferenceEquals(s1, s2));     //__________
			Console.WriteLine(str2);                        //__________
			Console.WriteLine(ReferenceEquals(str2, str1)); //__________
			Console.WriteLine(i2);                          //__________
			Console.WriteLine(ReferenceEquals(i1, i2));     //__________

			Console.WriteLine("Compelte.");
		}


		//static void Main(string[] args)
		//{
		//	BaiduSearchSpider spider = new BaiduSearchSpider();
		//	spider.Run();
		//	Console.WriteLine("Compelte.");
		//}
	}
}
