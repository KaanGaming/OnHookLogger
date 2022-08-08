using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace OnHookLogger
{
	public class MethodUtil
	{
		public MethodUtil(string asmName)
		{
			AssemblyName aName = new AssemblyName(asmName);
			AssemblyBuilder asmb = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run);
			ModuleBuilder mb = asmb.DefineDynamicModule(aName.Name);
			TypeBuilder tb = mb.DefineType("Handler", TypeAttributes.Class);

			_handlerType = tb;
		}

		private TypeBuilder _handlerType;
		public Type? finalProduct;

		private List<EventSearchResult> _trackedMethods = new(ushort.MaxValue);

		private MethodBuilder DefineMethod(string name, Type returnType, params Type[] paramTypes)
		{
			return _handlerType.DefineMethod(name, MethodAttributes.Static | MethodAttributes.Public,
				CallingConventions.Standard, returnType, paramTypes);
		}

		private (MethodInfo mi, Type[] pars) GetDelegate(EventInfo e)
		{
			Type dele = e.EventHandlerType;
			MethodInfo invoker = GetInvoker(dele);

			ParameterInfo[] parInfos = invoker.GetParameters();
			Type[] pars = new Type[parInfos.Length];
			for (int i = 0; i < pars.Length; i++)
			{
				pars[i] = parInfos[i].ParameterType;
			}

			return (invoker, pars);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="t">Must be a <see cref="Delegate"/></param>
		/// <returns></returns>
		private MethodInfo GetInvoker(Type t)
		{
			return t.GetMethod("Invoke") ?? throw new ArgumentException("Type doesn't contain Invoke method");
		}
		
		public void CreateListener(EventSearchResult e, Action callback)
		{
			if (_trackedMethods.Contains(e))
				return;

			var dele = GetDelegate(e.Event);
			MethodBuilder h = DefineMethod($"{string.Join("_", e.DeclaringTypes)}_{e.Event.Name}_Handler", dele.mi.ReturnType, dele.pars);

			//int paramsNum = dele.pars.Length;

			//ILGenerator il = h.GetILGenerator();
			//il.Emit(OpCodes.Call, callback.GetMethodInfo());
			//il.Emit(OpCodes.Ldarg_0); // orig
			//if (paramsNum > 1)
			//{
			//	for (int i = 1; i < paramsNum; i++)
			//		il.Emit(OpCodes.Ldarg_S, i); // the remaining variables that the On. hook may have
			//}
			//il.Emit(OpCodes.Callvirt, dele.orig); // orig(other parameters like self, etc.)
			//il.Emit(OpCodes.Ret);

			MethodType mType = MethodType.None;
			if (dele.mi.ReturnType != null)
				mType = mType | MethodType.Returns;
			if (dele.pars.Length > 0)
				mType = mType | MethodType.HasArgs;

			MethodInfo callOrig = typeof(ILHelper).GetMethod("CallOrig");
			MethodInfo callOrigRet = typeof(ILHelper).GetMethod("CallOrigWithReturn");
			MethodInfo callOrigArgs = typeof(ILHelper).GetMethod("CallOrigWithArgs");
			MethodInfo callOrigArgsRet = typeof(ILHelper).GetMethod("CallOrigWithArgsAndReturn");

			ILGenerator il = h.GetILGenerator();
			il.Emit(OpCodes.Call, callback.GetMethodInfo());

			void LoadArguments(ILGenerator il, int paramsCount, int startingIndex = 0)
			{
				for (int i = startingIndex; i < paramsCount; i++)
					il.Emit(OpCodes.Ldarg_S, i);
			}

			int paramsCount = dele.pars.Length;

			switch (mType)
			{
				case MethodType.None:
					il.Emit(OpCodes.Ldarg_0);
					il.EmitCall(OpCodes.Call, callOrig, null);
					break;
				case MethodType.HasArgs:
					LoadArguments(il, paramsCount);
					il.EmitCall(OpCodes.Call, callOrigArgs, dele.pars);
					break;
				case MethodType.Returns:
					LoadArguments(il, paramsCount);
					il.EmitCall(OpCodes.Callvirt, callOrigRet, null);
					break;
				case MethodType.HasArgs | MethodType.Returns:
					LoadArguments(il, paramsCount);
					il.EmitCall(OpCodes.Callvirt, callOrigArgsRet, dele.pars);
					break;
			}

			il.Emit(OpCodes.Ret);

			_trackedMethods.Add(e);
		}

		public void FinalizeType()
		{
			finalProduct = _handlerType.CreateType();
		}

		/// <summary>
		/// Must call <see cref="FinalizeType"/> before using this method
		/// </summary>
		/// <param name="declaringType"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public MethodInfo? GetListener(EventSearchResult e)
		{
			if (finalProduct == null)
				throw new Exception("MethodUtil's FinalizeType was not called");

			try
			{
				return finalProduct.GetMethod($"{string.Join("_", e.DeclaringTypes)}_{e.Event.Name}_Handler");
			}
			catch (AmbiguousMatchException)
			{
				throw new ArgumentException($"Ambiguous match was found for {e}");
			}
		}

		public MethodInfo? GetListenerSafe(EventSearchResult e, Action<bool, string?> callback)
		{
			if (finalProduct == null)
				throw new Exception("MethodUtil's FinalizeType was not called");

			MethodInfo? method = null;

			try
			{
				method = finalProduct.GetMethod($"{string.Join("_", e.DeclaringTypes)}_{e.Event.Name}_Handler");
			}
			catch (AmbiguousMatchException)
			{
				callback(false, $"Ambiguous match was found for {e}");
			}

			if (method != null)
				callback(true, null);

			return method;
		}
	}

	[Flags]
	public enum MethodType
	{
		None = 0b_00,
		HasArgs = 0b_01,
		Returns = 0b_10
	}
}
