using System;

namespace OnHookLogger.Exceptions
{
	public class LdobjResultNullException : Exception
	{
		public LdobjResultNullException(string caller, string message) : base($"from {caller} - {message}")
		{

		}
	}
}