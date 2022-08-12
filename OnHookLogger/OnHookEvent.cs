using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OnHookLogger
{
	public class OnHookEvent
	{
		public OnHookEvent(string classFullname, bool isStatic)
		{
			className = classFullname;
			this.isStatic = isStatic;
			currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
			time = startupTime + TimeSpan.FromSeconds(Time.time);
		}

		public static TimeSpan startupTime;

		public string className;
		public bool isStatic;
		public Scene currentScene;
		public TimeSpan time;

		public override string ToString()
		{
			return $"[{time}]" + className + (isStatic ? " (Static event)" : "")
			                 + $" - Scene name {currentScene.name} (Path: {currentScene.path})";
		}
	}
}