using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using GlobalEnums;
using JetBrains.Annotations;

namespace OnHookLogger
{
	public class MethodUtil
	{
		public MethodUtil(string asmName)
		{
			AssemblyName aName = new AssemblyName(asmName);
			AssemblyBuilder asmb = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.RunAndSave);
			ModuleBuilder mb = asmb.DefineDynamicModule(aName.Name);
			TypeBuilder tb = mb.DefineType("Handler", TypeAttributes.Class);

			_asmB = asmb;
			_handlerType = tb;
		}

		private AssemblyBuilder _asmB;
		private TypeBuilder _handlerType;
		public Type? finalProduct;

		private List<EventSearchResult> _trackedMethods = new(ushort.MaxValue);

		private MethodBuilder DefineMethod(string name, Type returnType, params Type[] paramTypes)
		{
			return _handlerType.DefineMethod(name, MethodAttributes.Static | MethodAttributes.Public,
				CallingConventions.Standard, returnType, paramTypes);
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
		
		public void CreateListener(EventSearchResult e, MethodInfo callback)
		{
			if (_trackedMethods.Contains(e))
				return;

			var hookInfo = new HookInfo(e.Event);
			MethodBuilder h = DefineMethod($"{string.Join("_", e.DeclaringTypes)}_{e.Event.Name}_Handler",
				hookInfo.invoker.ReturnType, hookInfo.paramTypes);

			int paramsNum = hookInfo.ParameterCount;
			MethodInfo orig = hookInfo.GetOrig();

			ILGenerator il = h.GetILGenerator();
			
			il.Emit(OpCodes.Ldstr, e.ToString());
			il.Emit(OpCodes.Call, callback); // non static methods need not to be called with callvirt
			
			for (int i = 0; i < paramsNum; i++)
			{
				il.Emit(OpCodes.Ldarg, i);
			}
			il.Emit(OpCodes.Callvirt, orig);
			il.Emit(OpCodes.Ret);

			_trackedMethods.Add(e);
		}

		public void FinalizeType()
		{
			_asmB.Save("test.dll");
			finalProduct = _handlerType.CreateType();
		}

		private void TestIL(On.HeroController.orig_Attack orig, HeroController self, AttackDirection dir)
		{
			orig(self, dir);
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
