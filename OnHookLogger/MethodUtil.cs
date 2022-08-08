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
			MethodInfo? invoker = dele.GetMethod("Invoke");
			if (invoker == null)
				throw new ArgumentException("Provided event's delegate has no method called Invoke");

			ParameterInfo[] parInfos = invoker.GetParameters();
			Type[] pars = new Type[parInfos.Length];
			for (int i = 0; i < pars.Length; i++)
			{
				pars[i] = parInfos[i].ParameterType;
			}

			return (invoker, pars);
		}
		
		public void CreateListener(EventSearchResult e, Action callback)
		{
			if (_trackedMethods.Contains(e))
				return;

			var dele = GetDelegate(e.Event);
			MethodBuilder h = DefineMethod($"{string.Join("_", e.DeclaringTypes)}_{e.Event.Name}_Handler", dele.mi.ReturnType, dele.pars);

			int paramsNum = dele.pars.Length;

			ILGenerator il = h.GetILGenerator();
			il.Emit(OpCodes.Call, callback.GetMethodInfo());
			il.Emit(OpCodes.Ldarg_0); // orig
			il.Emit(OpCodes.Ldarg_1); // self
			if (paramsNum > 2)
			{
				for (int i = 2; i < paramsNum; i++)
					il.Emit(OpCodes.Ldarg_S, i); // the remaining variables that the On. hook may have
			}
			il.Emit(OpCodes.Callvirt, dele.mi); // orig(self, ...remaining parameters...)
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
}
