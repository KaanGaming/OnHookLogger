using Modding;
using System;
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

		// if you need preloads, you will need to implement GetPreloadNames and use the other signature of Initialize.
		public override void Initialize()
		{
			Log("Initializing");

			// put additional initialization logic here
			AttachLoggersToEvents();

			Log("Initialized");
		}

		private void AttachLoggersToEvents()
		{
			foreach (Type t in GetHookTypes())
			{
				foreach (EventInfo e in t.GetEvents())
				{
					// subscribe to event in e
					// then create a method that will log whenever that event happens
				}
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
