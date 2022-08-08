using Modding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
			On.HeroController.AddGeo += HeroController_AddGeo; // TODO: PLACEHOLDER, delete branch after the job is complete
			AttachLoggersToEvents();

			Log("Initialized");
		}

		private void HeroController_AddGeo(On.HeroController.orig_AddGeo orig, HeroController self, int amount)
		{
			orig(self, amount);
		}

		private void AttachLoggersToEvents()
		{
			List<EventSearchResult> eList = SearchEvents();

			foreach (EventSearchResult e in eList)
			{
				_methodUtil.CreateListener(e, delegate
				{
					Log($"{string.Join(".", e.DeclaringTypes) + "." + e.Event.Name} was activated");
				});
			}

			_methodUtil.FinalizeType();

			foreach (EventSearchResult e in eList)
			{
				MethodInfo? handler = _methodUtil.GetListenerSafe(e, delegate(bool success, string? error)
				{
					if (!success)
						LogError(error);
				});
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
		
		private Type[] ReturnClasses(Type[] types)
		{
			return types.Where(x => x.IsClass && !x.IsSubclassOf(typeof(Delegate))).ToArray();
		}

		private List<EventSearchResult> SearchEvents()
		{
			var results = new List<EventSearchResult>();
			
			foreach (Type t in ReturnClasses(GetHookTypes()))
			{
				foreach (EventInfo e in t.GetEvents().Where(x => !x.Name.Contains("ctor")))
				{
					EventSearchResult result = new EventSearchResult(
						GetDeclaringTypes(e.DeclaringType), e);
					results.Add(result);
				}
			}

			string[] GetDeclaringTypes(Type type, List<string>? prevList = null)
			{
				List<string> typeList = prevList ?? new List<string>();
				if (prevList == null)
					typeList.Add(type.Name);

				if (type.DeclaringType != null)
				{
					typeList.Add(type.DeclaringType.Name);
					GetDeclaringTypes(type.DeclaringType, typeList);
				}
				return typeList.ToArray().Reverse().ToArray();
			}

			return results;
		}
	}
}
