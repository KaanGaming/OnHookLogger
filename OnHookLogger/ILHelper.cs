using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OnHookLogger
{
	public static class ILHelper
	{
		public static void CallOrig<T>(T origDelegate) where T : Delegate
		{
			origDelegate.DynamicInvoke();
		}

		public static object CallOrigWithReturn<T>(T origDelegate) where T : Delegate
		{
			return origDelegate.DynamicInvoke();
		}

		public static void CallOrigWithArgs<T>(T origDelegate, params object[] pars) where T : Delegate
		{
			origDelegate.DynamicInvoke(pars);
		}
		public static object CallOrigWithArgsAndReturn<T>(T origDelegate, params object[] pars) where T : Delegate
		{
			return origDelegate.DynamicInvoke(pars);
		}
	}
}
