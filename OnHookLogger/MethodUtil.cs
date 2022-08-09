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
		
		public void CreateListener(EventSearchResult e, Action<string> callback)
		{
			if (_trackedMethods.Contains(e))
				return;

			var hookInfo = new HookInfo(e.Event);
			MethodBuilder h = DefineMethod($"{string.Join("_", e.DeclaringTypes)}_{e.Event.Name}_Handler",
				hookInfo.invoker.ReturnType, hookInfo.paramTypes);

			int paramsNum = hookInfo.ParameterCount;

			MethodInfo orig = hookInfo.GetOrig();

			ILGenerator il = h.GetILGenerator();
			//il.Emit(OpCodes.Call, callback.GetMethodInfo());
			//il.Emit(OpCodes.Ldarg_0); // orig
			//if (paramsNum > 1)
			//{
			//	for (int i = 1; i < paramsNum; i++)
			//		il.Emit(OpCodes.Ldarg_S, i); // the remaining variables that the On. hook may have
			//}
			//il.Emit(OpCodes.Callvirt, dele.orig); // orig(other parameters like self, etc.)
			//il.Emit(OpCodes.Ret);

			//il.Emit(OpCodes.Call, callback.GetMethodInfo()); // Call the callback that the user of util has defined

			void log(string txt) // TODO: log functions are subject to delete later when branch work is done
			{
				if (h.Name == "Language_Get_string_string_Handler")
					OnHookLogger.Instance.Log(txt);
			}
			
			// BUG: calling the callback parameter of this function doesn't work
			
			log($"{e} - {paramsNum} parameters");
			string paramstxt = $"these parameters are: {string.Join(", ", hookInfo.paramTypes.Select(x => x.Name))}";
			log(paramstxt);
			
			il.Emit(OpCodes.Ldstr, e.ToString());
			il.Emit(OpCodes.Callvirt, callback.GetMethodInfo());
			
			log("Ldarg_0");
			il.Emit(OpCodes.Ldarg_0); // Push orig into the top of evaluation stack
			for (int i = 1; i < paramsNum; i++)
			{
				il.Emit(OpCodes.Ldarg_S, i); // Push other parameters to the eval stack
				log("Ldarg_S     " + i);
			}
			log($"Callvirt     {hookInfo.paramTypes[0].FullName}");
			il.Emit(OpCodes.Callvirt, orig); // Call delegate
			log("Ret");
			il.Emit(OpCodes.Ret); // Return the top of eval stack, if there's any return value returned by orig

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
