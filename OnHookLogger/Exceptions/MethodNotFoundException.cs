using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OnHookLogger.Exceptions
{
	public class MethodNotFoundException : Exception
	{
		public MethodNotFoundException(string message) : base(message)
		{

		}
	}
}
