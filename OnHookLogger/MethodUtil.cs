using OnHookLogger.Exceptions;
using OnHookLogger.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;

namespace OnHookLogger
{
	public class MethodUtil
	{
		public MethodUtil(string asmName)
		{
			AppDomain domain = Thread.GetDomain();
			AssemblyName aName = new AssemblyName(asmName);
			AssemblyBuilder asmb = domain.DefineDynamicAssembly(aName, AssemblyBuilderAccess.RunAndSave);
			ModuleBuilder mb = asmb.DefineDynamicModule("Module_" + aName.Name);
			TypeBuilder tb = mb.DefineType("Handler", TypeAttributes.Class);

			_asmB = asmb;
			_handlerType = tb;

			_handles = new List<GCHandle>();
		}

		~MethodUtil()
		{
			for (int i = _handles.Count - 1; i >= 0; i--)
			{
				_handles[i].Free();
				_handles.RemoveAt(i);
			}
		}

		private AssemblyBuilder _asmB;
		private TypeBuilder _handlerType;
		public Type? finalProduct;

		private List<GCHandle> _handles;

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
		/// <param name="callback">Method must be in the format of <c>f(OnHookEvent)</c> and must NOT return anything</param>
		/// <param name="cbTarget">The instance of the callback. If the callback method is static, this parameter goes unused
		/// and therefore can be left as <see langword="null"/>.</param>
		/// <exception cref="MethodNotFoundException"></exception>
		public void CreateListener(EventSearchResult e, MethodInfo callback, object cbTarget, Type cbType)
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
			bool isStatic = hookInfo.parameters.Length > 1 ? hookInfo.parameters[1].Name == "self" : false;
			MethodInfo orig = hookInfo.GetOrig();

			ILGenerator il = h.GetILGenerator();
			Label condLabel1 = il.DefineLabel();
			Label condLabel2 = il.DefineLabel();

			if (!callback.IsStatic)
			{
				_handles.Add(il.EmitObject(cbTarget, cbType));

				il.EmitCNull();
				il.EmitIf(CondType.False, () => // if (obj == null)
				{
					ConstructorInfo? exCtor = typeof(LdobjResultNullException).GetConstructor(
						new[] { typeof(string), typeof(string) });
					if (exCtor != null)
					{
						il.Emit(OpCodes.Ldstr, e.ToString());
						il.Emit(OpCodes.Ldstr, $"{h.Name}: Callback target is null");
						il.Emit(OpCodes.Newobj, exCtor);
						il.Emit(OpCodes.Throw);
					}
				}, condLabel1);
			}
				
			// instances of classes are immutable, therefore they're object references
			
			il.Emit(OpCodes.Ldstr, e.ToString());
			il.Emit(OpCodes.Ldc_I4, Convert.ToInt32(isStatic)); // bool
			il.Emit(OpCodes.Newobj, evCtor); // new OnHookEvent(string, bool)

			il.EmitCNull();
			il.EmitIf(CondType.False, () => // if (obj == null)
			{
				ConstructorInfo? exCtor = typeof(LdobjResultNullException).GetConstructor(
					new[] { typeof(string), typeof(string) });
				if (exCtor != null)
				{
					il.Emit(OpCodes.Ldstr, e.ToString());
					il.Emit(OpCodes.Ldstr, $"{h.Name}: Callback target is null");
					il.Emit(OpCodes.Newobj, exCtor);
					il.Emit(OpCodes.Throw);
				}
			}, condLabel2);
			
			if (callback.IsStatic)
				il.Emit(OpCodes.Call, callback); // Log(OnHookEvent ev)
			else
				il.Emit(OpCodes.Callvirt, callback); // The same, except this is an instance call

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
			finalProduct = _handlerType.CreateType();
			_asmB.Save("test.dll");
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
