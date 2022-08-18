using OnHookLogger.Exceptions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

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
		/// <param name="e"></param>
		/// <param name="callback">Method must be in the format of <c>f(<see cref="OnHookEvent"/>)</c></param>
		/// <exception cref="MethodNotFoundException"></exception>
		public void CreateListener(EventSearchResult e, MethodInfo callback)
		{
			if (_trackedMethods.Contains(e))
				return;

			var hookInfo = new HookInfo(e.Event);
			MethodBuilder h = DefineMethod($"{string.Join("_", e.DeclaringTypes)}_{e.Event.Name}_Handler",
				hookInfo.invoker.ReturnType, hookInfo.paramTypes);

			ConstructorInfo evCtor = typeof(OnHookEvent).GetConstructor(
				new[] { typeof(string), typeof(bool) }) 
			              ?? throw new MethodNotFoundException("Constructor of OnHookEvent is either out of date or not valid");

			int paramsNum = hookInfo.ParameterCount;
			bool isStatic = hookInfo.parameters[1].Name == "self";
			MethodInfo orig = hookInfo.GetOrig();

			ILGenerator il = h.GetILGenerator();
			
			il.Emit(OpCodes.Ldstr, e.ToString());
			il.Emit(OpCodes.Ldc_I4, Convert.ToInt32(isStatic)); // bool
			il.Emit(OpCodes.Newobj, evCtor); // new OnHookEvent(string, bool)

			// TODO: Push an object reference to the method

			//il.Emit(OpCodes.Call, log); // Log(int index, OnHookEvent ev)

			//il.Emit(OpCodes.Ldstr, e.ToString());
			//il.Emit(OpCodes.Call, callback); // non static methods need not to be called with callvirt
			
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

		/// <summary>
		/// Must call <see cref="FinalizeType"/> before using this method
		/// </summary>
		public MethodInfo? GetListener(EventSearchResult e)
		{
			if (finalProduct == null)
				throw new TypeNotFinalizedException("MethodUtil's FinalizeType was not called");
			
			return finalProduct.GetMethod($"{string.Join("_", e.DeclaringTypes)}_{e.Event.Name}_Handler");
		}

		public MethodInfo? GetListenerSafe(EventSearchResult e, Action<bool, string?> callback)
		{
			MethodInfo? method = null;

			try
			{
				method = GetListener(e);
			}
			catch (AmbiguousMatchException)
			{
				callback(false, $"Ambiguous match was found for {e}");
			}
			catch (TypeNotFinalizedException ex)
			{
				callback(false, ex.Message);
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
