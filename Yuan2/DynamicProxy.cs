using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace YuanYuan
{
	/// <summary>
	/// This class construct a proxy object for you based on given object, additional methods can be added to the proxy object
	/// and additional behavior can be added to all methods
	/// Similar to AOP
	/// See example on public methods
	/// </summary>
	public class DynamicProxy
	{
		/// <summary>
		/// Wrap given target object, add new methods (additionalInterfacesToProxy), inject code to specified methods (targetType)
		/// </summary>
		/// <param name="targert">object to be wrapperred</param>
		/// <param name="additionalInterfacesToProxy">additional interfaces that you want to implement in the wrapper object, the implementation is in Interceptor</param>
		/// <param name="interceptor">delegate call that will interrupt original object's methods to provide additional behavior</param>
		/// <example>
		/// Sample to call the method:
		///	1. Impement a interceptor:
		///		public class Interceptor : IInterceptor
		///		{
		///			public object Call(string methodInfo, MulticastDelegate methodDelegate, params object[] args)
		///			{
		///				// 1. Before call the target method, you can do something.
		///				// 2. Sometimes, we do not need to call back the method, we reture the value immediately.
		///				// 3. We need to handle all methods which in the additional interfaces.
		///				      the patter could be.
		///				      if(methodname == "addtional method")
		///						{ //your implementation }
		///				return methodDelegate.Method.Invoke(methodDelegate.Target, args);
		///			}
		///		}
		///		
		///	2. Create a proxy object:
		///		SomeType targetType= new SomeType();
		///		Type[] additionalInterfacesToProxy = new Type[1] { typeof(IAdditional) };
		///		ProxyGenerator proxyGenerator = new ProxyGenerator();
		///		object obj = proxyGenerator.Wrap(targetType, additionalInterfacesToProxy, new Interceptor());
		///		
		///	3. Convert the obj to what type/interface you want
		///		SomeType st = (SomeType)obj;
		///		or 
		///		IAdditional ia= (IAdditional)obj;
		///		
		///	4. Limitation: 
		///    It will only intercept the virtual method in the target type, 
		///    which means methods in targetType you want to intercept must be marked as virtual
		/// </example>
		/// <returns>return wrappered object</returns>
		public Object CreateProxy(object targert, Type[] additionalInterfacesToProxy, IInterceptor interceptor)
		{
			Type proxyType = null;
			try
			{
				Type targetType = targert.GetType();
				AssemblyBuilder assemblyBuilder;

				//remove methods that already exist in target object from addtional interfaces to avoid duplicated
				Type[] realAdditionalInterfacesToProxy = GetAdditionalInterfaces(targetType, additionalInterfacesToProxy);

				//get all the public methods for proxy object, this including methods in target object and addtional interfaces
				MethodInfo[] allPublicMethodInfos = GetAllPublicMethods(targetType, realAdditionalInterfacesToProxy);

				//create the proxy type builder
				ModuleBuilder moduleBuilder = InitModule(targetType, out assemblyBuilder);
				TypeBuilder proxyTpeBuilder = moduleBuilder.DefineType(targetType.Name + "_" + targetType.GetHashCode(), TypeAttributes.Class | TypeAttributes.Public, targetType, additionalInterfacesToProxy);

				//define the delegate types and constructors for all virtual methods
				TypeBuilder[] nestedTypeBuilders = GenerateDelegateClass(proxyTpeBuilder, allPublicMethodInfos);

				// define variables for all virtual methods as delegate type
				FieldBuilder[] multiCastDelegates = GenerateFields(proxyTpeBuilder, allPublicMethodInfos, nestedTypeBuilders);

				// define variable of target type, then we can call methods in the target type
				FieldBuilder targertBuilder = proxyTpeBuilder.DefineField("__" + targetType.Name, targetType, FieldAttributes.Private);

				// define methods that will call into given target object's method
				MethodBuilder[] callBackMethods = GenerateCallBackMethods(targetType, proxyTpeBuilder, realAdditionalInterfacesToProxy, targertBuilder);

				// define the IInterceptor
				FieldBuilder interceptorFiledBuilder = proxyTpeBuilder.DefineField("__Interceptor", typeof(IInterceptor), FieldAttributes.Private);

				//define public methods
				GenerateOverrideMethods(proxyTpeBuilder, allPublicMethodInfos, interceptorFiledBuilder, multiCastDelegates);

				//define constructor
				GenerateConstructor(targetType, proxyTpeBuilder, nestedTypeBuilders, interceptorFiledBuilder, callBackMethods, multiCastDelegates, targertBuilder);

				//generate wrapper class content including methods, fileds and constructors
				proxyType = proxyTpeBuilder.CreateTypeInfo().AsType();

				//generate delegate types
				foreach (TypeBuilder tb in nestedTypeBuilders)
				{
					if (tb != null)
					{
						tb.CreateTypeInfo().AsType();
					}
				}

				//// save this assembly, for test only
				//assemblyBuilder.Save("DynamicProxy.Temp.dll");

				// Create the proxy object
				return Activator.CreateInstance(proxyType, targert, interceptor);
			}
			catch (Exception err)
			{
				throw new Exception("Caught Exception trying to wrapper object using dynamic proxy:" + err);
			}
		}

		private ModuleBuilder InitModule(Type targetType, out AssemblyBuilder assemblyBuilder)
		{
			AppDomain domain = AppDomain.CurrentDomain;
			AssemblyName asmName = new AssemblyName(targetType.Name + "_" + targetType.GetHashCode());
			assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);
			ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(targetType.Name + "_" + targetType.GetHashCode() + ".ddl");
			return moduleBuilder;
		}

		private TypeBuilder[] GenerateDelegateClass(TypeBuilder proxyTpeBuilder, MethodInfo[] allPublicMethodInfos)
		{
			TypeBuilder[] nestedTypeBuilders = new TypeBuilder[allPublicMethodInfos.Length];

			// create Nested calss for every methods
			for (Int32 i = 0; i < nestedTypeBuilders.Length; i++)
			{
				// only the virtual method can be proxy
				if (allPublicMethodInfos[i].IsVirtual)
				{
					// create Nested Class
					nestedTypeBuilders[i] = proxyTpeBuilder.DefineNestedType("__" + allPublicMethodInfos[i].Name + "_" + allPublicMethodInfos[i].GetHashCode() + "__delegate", TypeAttributes.NestedPrivate | TypeAttributes.Sealed, typeof(MulticastDelegate));

					Type[] argsType = GetParameterTypes(allPublicMethodInfos[i]);

					// create the invoke for the method
					MethodBuilder mb = nestedTypeBuilders[i].DefineMethod("Invoke", MethodAttributes.Public, CallingConventions.Standard, allPublicMethodInfos[i].ReturnType, argsType);
					mb.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);
				}
			}

			return nestedTypeBuilders;
		}
		// generate the private fields in the proxy type
		private FieldBuilder[] GenerateFields(TypeBuilder proxyTpeBuilder, MethodInfo[] allPublicMethodInfos, TypeBuilder[] nestedTypeBuilders)
		{
			// create fields for every Nexted Class
			FieldBuilder[] multiCastDelegates = new FieldBuilder[allPublicMethodInfos.Length];
			for (Int32 i = 0; i < allPublicMethodInfos.Length; i++)
			{
				if (allPublicMethodInfos[i].IsVirtual)
				{
					multiCastDelegates[i] = proxyTpeBuilder.DefineField(allPublicMethodInfos[i].Name + "_field", nestedTypeBuilders[i], FieldAttributes.Private);
				}
			}
			// methodInfosField = proxyTpeBuilder.DefineField("__methodInfos", typeof(List<MethodInfo>), FieldAttributes.Private);

			return multiCastDelegates;
		}

		private MethodBuilder[] GenerateCallBackMethods(Type targetType, TypeBuilder proxyTpeBuilder, Type[] additionalInterfacesToProxy, FieldBuilder targertBuilder)
		{
			List<MethodInfo> targetMethoInfos = new List<MethodInfo>(targetType.GetMethods());

			List<MethodInfo> additionalInterfacesMethods = new List<MethodInfo>();
			foreach (Type type in additionalInterfacesToProxy)
			{
				additionalInterfacesMethods.AddRange(type.GetMethods());
			}

			MethodBuilder[] callBackMethods = new MethodBuilder[targetMethoInfos.Count + additionalInterfacesMethods.Count];
			for (Int32 i = 0; i < targetMethoInfos.Count; i++)
			{
				if (targetMethoInfos[i].IsVirtual)
				{
					// get the paramters of this method
					Type[] argTypes = GetParameterTypes(targetMethoInfos[i]);
					// define the call back method
					callBackMethods[i] = proxyTpeBuilder.DefineMethod("callback_" + targetMethoInfos[i].Name, MethodAttributes.Private, CallingConventions.Standard, targetMethoInfos[i].ReturnType, argTypes);

					ILGenerator ilGenerator = callBackMethods[i].GetILGenerator();
					// push this pointer to stack
					ilGenerator.Emit(OpCodes.Ldarg_0);
					// push the target object to stack
					ilGenerator.Emit(OpCodes.Ldfld, targertBuilder);
					// push all the paramters to stack
					for (Int32 j = 0; j < argTypes.Length; j++)
					{
						ilGenerator.Emit(OpCodes.Ldarg, j + 1);
					}
					// call the method
					ilGenerator.Emit(OpCodes.Callvirt, targetMethoInfos[i]);
					// return
					ilGenerator.Emit(OpCodes.Ret);
				}
			}

			// we don't implement additional interface here, the real implmentation is in interceptor class.
			for (Int32 i = 0; i < additionalInterfacesMethods.Count; i++)
			{
				Type[] argTypes = GetParameterTypes(additionalInterfacesMethods[i]);
				callBackMethods[i + targetMethoInfos.Count] = proxyTpeBuilder.DefineMethod("callback_" + additionalInterfacesMethods[i].Name, MethodAttributes.Private, CallingConventions.Standard, additionalInterfacesMethods[i].ReturnType, argTypes);

				ILGenerator ilGenerator = callBackMethods[i + targetMethoInfos.Count].GetILGenerator();
				ilGenerator.Emit(OpCodes.Nop);
				ilGenerator.Emit(OpCodes.Ldstr, "Method " + additionalInterfacesMethods[i].Name + " is not handled, please hanlde it in IInterceptor");
				ilGenerator.Emit(OpCodes.Newobj, typeof(AdditionalMethodNotImplementedException).GetConstructor(new Type[1] { typeof(string) }));
				ilGenerator.Emit(OpCodes.Throw);
			}

			return callBackMethods;
		}

		// overide all the methods which need t proxy
		private void GenerateOverrideMethods(TypeBuilder proxyTpeBuilder, MethodInfo[] allPublicMethodInfos, FieldBuilder interceptorFiledBuilder, FieldBuilder[] multiCastDelegates)
		{
			for (Int32 i = 0; i < allPublicMethodInfos.Length; i++)
			{
				if (allPublicMethodInfos[i].IsVirtual)
				{
					Type[] argTypes = GetParameterTypes(allPublicMethodInfos[i]);
					MethodBuilder mb = proxyTpeBuilder.DefineMethod(allPublicMethodInfos[i].Name, MethodAttributes.Public | MethodAttributes.Virtual, CallingConventions.Standard, allPublicMethodInfos[i].ReturnType, argTypes);

					ILGenerator ilGenerator = mb.GetILGenerator();
					// push the interceptor to stack
					ilGenerator.Emit(OpCodes.Ldarg_0);
					ilGenerator.Emit(OpCodes.Ldfld, interceptorFiledBuilder);
					// push the first paramter to stack
					ilGenerator.Emit(OpCodes.Ldstr, allPublicMethodInfos[i].Name);
					// push the second paramter to stack
					ilGenerator.Emit(OpCodes.Ldarg_0);
					ilGenerator.Emit(OpCodes.Ldfld, multiCastDelegates[i]);
					// push the third paramter to stack
					LocalBuilder local = ilGenerator.DeclareLocal(typeof(Object[]));
					ilGenerator.Emit(OpCodes.Ldc_I4, argTypes.Length);
					ilGenerator.Emit(OpCodes.Newarr, typeof(Object));
					ilGenerator.Emit(OpCodes.Stloc, local);
					ilGenerator.Emit(OpCodes.Ldloc, local);
					for (Int32 j = 0; j < argTypes.Length; j++)
					{
						ilGenerator.Emit(OpCodes.Ldc_I4, j);
						ilGenerator.Emit(OpCodes.Ldarg, j + 1);
						ilGenerator.Emit(OpCodes.Box, argTypes[j]);
						ilGenerator.Emit(OpCodes.Stelem_Ref);
						ilGenerator.Emit(OpCodes.Ldloc, local);
					}

					// Call the interceptor
					ilGenerator.Emit(OpCodes.Call, typeof(IInterceptor).GetMethod("Call", new Type[] { typeof(string), typeof(MulticastDelegate), typeof(Object[]) }));
					if (allPublicMethodInfos[i].ReturnType.Equals(typeof(void)))
					{
						ilGenerator.Emit(OpCodes.Pop);
					}
					else
					{
						ilGenerator.Emit(OpCodes.Unbox_Any, allPublicMethodInfos[i].ReturnType);
					}
					ilGenerator.Emit(OpCodes.Ret);
				}
			}
		}

		// generate the constructor for the proxy type
		private void GenerateConstructor(Type targetType, TypeBuilder proxyTpeBuilder, TypeBuilder[] nestedTypeBuilders, FieldBuilder interceptorFiledBuilder, MethodBuilder[] callBackMethods, FieldBuilder[] multiCastDelegates, FieldBuilder targertBuilder)
		{
			ConstructorBuilder constructorBuilder = proxyTpeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { targetType, typeof(IInterceptor) });
			ILGenerator ilGenerator = constructorBuilder.GetILGenerator();

			// call the default constructor firstly
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Call, targetType.GetConstructor(new Type[] { }));

			// save the target oject to the target field
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Ldarg_1);
			ilGenerator.Emit(OpCodes.Stfld, targertBuilder);

			// initialize __Interceptor
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Ldarg_2);
			ilGenerator.Emit(OpCodes.Stfld, interceptorFiledBuilder);

			// initialize Nested Class field
			for (Int32 i = 0; i < multiCastDelegates.Length; i++)
			{
				if (multiCastDelegates[i] != null)
				{
					ConstructorBuilder nestedTypeConstructor = nestedTypeBuilders[i].DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { typeof(Object), typeof(IntPtr) });
					nestedTypeConstructor.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

					ilGenerator.Emit(OpCodes.Ldarg_0);
					ilGenerator.Emit(OpCodes.Ldarg_0);
					ilGenerator.Emit(OpCodes.Ldftn, callBackMethods[i]);
					ilGenerator.Emit(OpCodes.Newobj, nestedTypeConstructor);
					ilGenerator.Emit(OpCodes.Stfld, multiCastDelegates[i]);
				}
			}

			//// initialize MethodInfosField
			//ilGenerator.Emit(OpCodes.Ldarg_0);
			//ilGenerator.Emit(OpCodes.Ldarg_0);
			//ilGenerator.Emit(OpCodes.Callvirt, typeof(Object).GetMethod("GetType", new Type[] { }));
			//ilGenerator.Emit(OpCodes.Callvirt, typeof(Type).GetMethod("GetMethods", new Type[] { }));
			//ilGenerator.Emit(OpCodes.Newobj, typeof(List<MethodInfo>).GetConstructor(new Type[] { typeof(MethodInfo[]) }));
			//ilGenerator.Emit(OpCodes.Stfld, methodInfosField);

			ilGenerator.Emit(OpCodes.Ret);
		}

		private Type[] GetAdditionalInterfaces(Type targetType, Type[] additionalInterfacesToProxy)
		{
			List<Type> types = new List<Type>();
			foreach (Type type in additionalInterfacesToProxy)
			{
				if (targetType.GetInterface(type.Name) == null)
				{
					types.Add(type);
				}
			}
			return types.ToArray();
		}

		private MethodInfo[] GetAllPublicMethods(Type targetType, Type[] additionalInterfacesToProxy)
		{
			// All the public methods in target type.
			List<MethodInfo> methodInfos = new List<MethodInfo>(targetType.GetMethods());

			// All the methods in additional interface
			foreach (Type type in additionalInterfacesToProxy)
			{
				methodInfos.AddRange(type.GetMethods());
			}

			return methodInfos.ToArray();
		}

		// get all the parameters of this MethodInfo
		private Type[] GetParameterTypes(MethodInfo methodInfo)
		{
			ParameterInfo[] args = methodInfo.GetParameters();
			Type[] argsType = new Type[args.Length];
			for (Int32 j = 0; j < args.Length; j++) { argsType[j] = args[j].ParameterType; }
			return argsType;
		}
	}

	/// <summary>
	/// The interceptor interface
	/// </summary>
	public interface IInterceptor
	{
		/// <summary>
		/// implementate the interceptor
		/// </summary>
		/// <param name="methodName">the method's name in the target type</param>
		/// <param name="methodDelegate">the delegate which can call back to method in the target object</param>
		/// <param name="args">the args for call back to method in the target object</param>
		/// <returns></returns>
		Object Call(string methodName, MulticastDelegate methodDelegate, params Object[] args);
	}

	public class AdditionalMethodNotImplementedException : Exception
	{
		public AdditionalMethodNotImplementedException(string msg)
			: base(msg)
		{
		}
	}
}
