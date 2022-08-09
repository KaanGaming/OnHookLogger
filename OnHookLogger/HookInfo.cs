using System;
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
			throw new NotImplementedException();
		}

		public Type[] GetParamTypes()
		{
			throw new NotImplementedException();
		}

		public int ParameterCount => throw new NotImplementedException();
	}
}