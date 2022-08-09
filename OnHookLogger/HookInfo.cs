using System;
using System.Linq;
using System.Reflection;

namespace OnHookLogger
{
	public class HookInfo
	{
		public HookInfo(EventInfo ev)
		{
			eventInfo = ev;
			invoker = ev.EventHandlerType.GetMethod("Invoke")
			          ?? throw new ArgumentException("Event Handler Type is not a delegate");
			parameters = invoker.GetParameters();
			paramTypes = GetParamTypes();
		}

		public EventInfo eventInfo;
		public MethodInfo invoker;
		public ParameterInfo[] parameters;
		public Type[] paramTypes;

		public MethodInfo GetOrig()
		{
			return paramTypes[0].GetMethod("Invoke")
				?? throw new InvalidOperationException("The first parameter of hook is not a delegate (orig)");
		}

		public Type[] GetParamTypes()
		{
			return parameters.Select(x => x.ParameterType).ToArray();
		}

		public int ParameterCount => parameters.Length;
	}
}