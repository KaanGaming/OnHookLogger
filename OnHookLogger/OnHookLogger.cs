using Modding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using OnHookLogger.Exceptions;
using UnityEngine;

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

		private Logger _logger;
		
		public OnHookLogger() : base("On. Hook Logger")
		{
			_instance = this;
			_logger = new Logger(GenerateLogFile());
		}

		private MethodUtil? _methodUtil;
		private Stopwatch _sw = new();
		private Dictionary<string, TimeSpan> _lastTriggers = new();

		private void LogStopwatch(string job, string category = "main")
		{
			if (!_lastTriggers.ContainsKey(category))
				_lastTriggers.Add(category, TimeSpan.Zero);
			Log($"Job {job} completed in {_sw.Elapsed - _lastTriggers[category]}");
			_lastTriggers[category] = _sw.Elapsed;
		}

		private string GenerateLogFile()
		{
			string locapd = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string folder = $"{locapd}/OnHookLogger";
			if (!Directory.Exists(folder))
				Directory.CreateDirectory(folder);
			DateTime now = DateTime.Now;

			int timeNumber = now.Hour * 3600 + now.Minute * 60 + now.Second;

			string filename = $"{folder}/log-{now.Day}-{now.Month}-{now.Year}-{timeNumber}.txt";
			if (!File.Exists(filename))
				File.WriteAllText(filename, "");
			return filename;
		}
		
		public override void Initialize()
		{
			Log("Initializing");

			OnHookEvent.startupTime = DateTime.Now.TimeOfDay;

			ModHooks.ApplicationQuitHook += OnAppClose;

			_sw.Start();
			_methodUtil = new MethodUtil("OnHookLoggerDynamic");
			LogStopwatch("MethodUtil Initialization");
			AttachLoggersToEvents();
			LogStopwatch("Attach Loggers to Events");

			Log("Initialized");
		}

		private void OnAppClose()
		{
			_logger.Close();
		}

		[UsedImplicitly]
		public void OnHookListener(OnHookEvent onHookEvent)
		{
			_logger.Log(onHookEvent);
		}

		private void AttachLoggersToEvents()
		{
			if (_methodUtil == null)
				return;

			List<EventSearchResult> eList = SearchEvents();

			foreach (EventSearchResult e in eList)
			{
				_methodUtil.CreateListener(e, GetType().GetMethod("OnHookListener") 
				        ?? throw new MethodNotFoundException("OnHookListener() in OnHookLogger doesn't exist"), this,
					typeof(OnHookLogger));
			}
			LogStopwatch("Create Listeners for On. Hooks", "attach");

			_methodUtil.FinalizeType();

			for (var i = 0; i < eList.Count; i++)
			{
				if (i % 1000 == 0 && i != 0)
					LogStopwatch($"Add listeners for 1000 events ({i} out of {eList.Count})", "addListener");

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
					if (tie.InnerException != null)
						LogError(tie.InnerException.Message);
					LogError(tie.StackTrace);
				}
			}

			LogStopwatch("Add Event Listeners", "attach");
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

			string[] GetDeclaringTypes(Type? type, List<string>? prevList = null)
			{
				if (type == null)
					return Array.Empty<string>();

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
