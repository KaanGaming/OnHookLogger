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
			List<EventSearchRecord> eList = SearchEvents();
			
			foreach (EventSearchRecord e in eList)
			{
				_methodUtil.CreateListener(e, delegate
				{
					Log($"{string.Join(".", e.DeclaringTypes) + "." + e.Event.Name} was activated");
				});
			}

			_methodUtil.FinalizeType();

			foreach (EventSearchRecord e in eList)
			{
				MethodInfo? handler = _methodUtil.GetListener(e, e.Event.Name);
				if (handler == null)
					continue;

				var d = Delegate.CreateDelegate(e.Event.EventHandlerType, handler);
				e.Event.AddEventHandler(null, d);
			}
		}

		private Type[] GetHookTypes()
		{
			return Assembly.GetAssembly(typeof(On.Achievement)).GetTypes()
				.Where(t => t.Namespace != null && t.Namespace.StartsWith("On"))
				.ToArray();
		}

		private List<EventSearchRecord> SearchEvents()
		{
			List<EventSearchRecord> results = new List<EventSearchRecord>();

			void Recursive(Type[] types)
			{
				foreach (Type t in types)
				{
					foreach (EventInfo e in t.GetEvents().Where(x => !x.Name.Contains("ctor")))
					{
						results.Add(new EventSearchRecord(GetDeclaringTypes(e.DeclaringType), e));
					}

					Type[] nestedTypes = t.GetNestedTypes();
					if (nestedTypes.Length != 0)
						Recursive(nestedTypes);
				}
			}

			string[] GetDeclaringTypes(Type type, List<string>? prevList = null)
			{
				List<string> typeList = prevList ?? new List<string>();

				if (type.DeclaringType != null)
				{
					typeList.Add(type.DeclaringType.Name);
					GetDeclaringTypes(type.DeclaringType, typeList);
				}
				return typeList.ToArray();
			}

			Recursive(GetHookTypes());

			return results;
		}
	}
}
