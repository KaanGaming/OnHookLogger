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

			// TODO: add timer ticking from start of attachment to the end of attachment
			// put additional initialization logic here
			_methodUtil = new MethodUtil("OnHookLoggerDynamic");
			AttachLoggersToEvents();

			Log("Initialized");
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

			for (var i = 0; i < eList.Count; i++)
			{
				if (i % 100 == 0)
					Log($"Attach progress: {i}/{eList.Count}");

				EventSearchResult e = eList[i];
				MethodInfo? handler = _methodUtil.GetListenerSafe(e, delegate(bool success, string? error)
				{
					if (!success)
						LogError(error);
				});
				if (handler == null)
					continue;

				try
				{
					var d = Delegate.CreateDelegate(e.Event.EventHandlerType, handler);
					MethodInfo addMethod = e.Event.GetAddMethod(true);
					addMethod.Invoke(null, new object[] { d });
					// instead of using the normal e.Event.AddEventHandler, i use this one because
					// the AddEventHandler throws if the AddMethod is not public, but this way it doesn't matter if it's
					// public or not
				}
				catch (InvalidOperationException ioex)
				{
					LogError($"Error caused by {e}");
					LogError(ioex);
				}
				catch (TargetInvocationException tie)
				{
					LogError($"Error caused by {e}");
					LogError(tie.Message);
					LogError(tie.InnerException.Message);
					LogError(tie.StackTrace);
				}
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
			
			foreach (Type t in ReturnClasses(GetHookTypes()).Where(x => !IsDelegateType(x)))
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

		private bool IsDelegateType(Type onType)
		{
			return onType.GetMethods().Any(x => x.Name.Contains("Invoke"));
		}
	}
}
