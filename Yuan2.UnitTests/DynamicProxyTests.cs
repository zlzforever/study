using System;
using Xunit;
using YuanYuan;

namespace Yun2.UnitTests
{
	public class DynamicProxyTests
	{
		public interface IMyTest
		{
			string GetConnectString();
		}

		public interface IAdditional
		{
			string GetInt();
			void GetDouble();
		}

		public class MyTest : IMyTest
		{
			public virtual string GetConnectString()
			{
				return "GetConnectString";
			}

			public string GetDefaultString()
			{
				return "Hello word";
			}
		}

		public class TestInterceptor : IInterceptor
		{
			public object TargetValue { get; set; }

			public object Call(string methodInfo, MulticastDelegate methodDelegate, params object[] args)
			{
				if (methodInfo.Equals("GetInt"))
				{
					TargetValue = "TargetValue";
				}
				else
				{
					TargetValue = methodDelegate.Method.Invoke(methodDelegate.Target, args);
				}
				return "TestIntercetor";
			}
		}

		/// <summary>
		/// The Test type implemente the interface ITest
		/// So test the proxy object can as the interface, and every method works well.
		/// </summary>
		[Fact]
		public void TestInterfaceFuction()
		{
			DynamicProxy proxyGenerator = new DynamicProxy();
			MyTest t = new MyTest();
			Type[] additionalInterfacesToProxy = new Type[1] { typeof(IMyTest) };
			TestInterceptor testIntercetor = new TestInterceptor();

			object obj = proxyGenerator.CreateProxy(t, additionalInterfacesToProxy, testIntercetor);
			IMyTest it = (IMyTest)obj;

			Assert.Equal("TestIntercetor", it.GetConnectString());
			Assert.Equal("GetConnectString", testIntercetor.TargetValue);
		}

		/// <summary>
		/// Test the proxy object can as the target type, and every method works well.
		/// </summary>
		[Fact]
		public void TestTargetFuction()
		{
			DynamicProxy proxyGenerator = new DynamicProxy();

			MyTest t = new MyTest();

			TestInterceptor testIntercetor = new TestInterceptor();
			object obj = proxyGenerator.CreateProxy(t, new[] { typeof(IMyTest) }, testIntercetor);
			MyTest it = (MyTest)obj;

			testIntercetor.TargetValue = null;
			Assert.Equal("TestIntercetor", it.GetConnectString());
			Assert.Equal("GetConnectString", testIntercetor.TargetValue);
			testIntercetor.TargetValue = null;
			Assert.Equal("Hello word", it.GetDefaultString());
			Assert.Null(testIntercetor.TargetValue);
		}

		/// <summary>
		/// Test a additional interface's method which has been handled works well.
		/// </summary>
		[Fact]
		public void TestHandledAdditionalFuction()
		{
			DynamicProxy proxyGenerator = new DynamicProxy();
			MyTest t = new MyTest();
			TestInterceptor testIntercetor = new TestInterceptor();
			object obj = proxyGenerator.CreateProxy(t, new[] { typeof(IAdditional) }, testIntercetor);
			IAdditional it = (IAdditional)obj;

			Assert.Equal("TestIntercetor", it.GetInt());
			Assert.Equal("TargetValue", testIntercetor.TargetValue);
		}

		/// <summary>
		/// Test a additional interface's method which has not been handled can throw the correctly exception.
		/// </summary>
		[Fact]
		public void TestNoHandleAdditionalFuction()
		{
			try
			{
				DynamicProxy proxyGenerator = new DynamicProxy();
				MyTest t = new MyTest();
				TestInterceptor testIntercetor = new TestInterceptor();
				object obj = proxyGenerator.CreateProxy(t, new[] { typeof(IAdditional), typeof(IMyTest) }, testIntercetor);
				IAdditional it = (IAdditional)obj;

				it.GetDouble();
			}
			catch (Exception ex)
			{
				Assert.Equal(typeof(AdditionalMethodNotImplementedException), ex.InnerException.GetType());
			}
		}
	}
}
