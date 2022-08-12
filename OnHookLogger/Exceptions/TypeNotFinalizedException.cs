using System;

namespace OnHookLogger.Exceptions
{
	public class TypeNotFinalizedException : Exception
	{
		public TypeNotFinalizedException(string message) : base(message)
		{

		}
	}
}