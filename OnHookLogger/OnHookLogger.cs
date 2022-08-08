using Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OnHookLogger
{
	public class OnHookLogger : Mod
	{
		private static OnHookLogger? _instance;

		internal static OnHookLogger Instance
		{
			get
			{
				if (_instance == null)
				{
					throw new InvalidOperationException($"An instance of {nameof(OnHookLogger)} was never constructed");
				}
				return _instance;
			}
		}

		public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

		public OnHookLogger() : base()
		{
			_instance = this;
		}

		private MethodUtil _methodUtil;

		// if you need preloads, you will need to implement GetPreloadNames and use the other signature of Initialize.
		public override void Initialize()
		{
			Log("Initializing");

			// put additional initialization logic here
			_methodUtil = new MethodUtil("OnHookLoggerDynamic");
			AttachLoggersToEvents();

			Log("Initialized");
		}

		private void AttachLoggersToEvents()
		{
			List<EventInfo> eList = new List<EventInfo>();

			// TODO: Search nested classes for events as well, ignore events named ctor (like the one in On.ObjectBounce.ctor cause apparently that causes problems?)
			foreach (Type t in GetHookTypes())
			{
				foreach (EventInfo e in t.GetEvents())
				{
					eList.Add(e);
				}
			}

			foreach (EventInfo e in eList)
			{
				_methodUtil.CreateListener(e, delegate
				{
					Log($"{e.Name} was activated");
				});
			}

			_methodUtil.FinalizeType();

			foreach (EventInfo e in eList)
			{
				MethodInfo? handler = _methodUtil.GetListener(e.DeclaringType.Name, e.Name);
				if (handler == null)
					continue;

				var d = Delegate.CreateDelegate(e.EventHandlerType, handler);
				e.AddEventHandler(null, d);
			}
		}

		private Type[] GetHookTypes()
		{
			return Assembly.GetAssembly(typeof(On.Achievement)).GetTypes()
				.Where(t => t.Namespace != null && t.Namespace.StartsWith("On"))
				.ToArray();
		}
	}
}
