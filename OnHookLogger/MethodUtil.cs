using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

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

		public void CreateListener(EventInfo e, Action callback)
		{
			var dele = GetDelegate(e);
			MethodBuilder h = DefineMethod($"{e.DeclaringType.Name}_{e.Name}_Handler", dele.mi.ReturnType, dele.pars);

			ILGenerator il = h.GetILGenerator();
			il.EmitCall(OpCodes.Call, callback.GetMethodInfo(), new Type[]{});
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
		public MethodInfo? GetListener(string declaringType, string name)
		{
			if (finalProduct == null)
				throw new Exception("MethodUtil's FinalizeType was not called");
			return finalProduct.GetMethod($"{declaringType}_{name}_Handler");
		}
	}
}
