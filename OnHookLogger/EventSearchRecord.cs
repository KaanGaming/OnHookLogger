using System.Reflection;

namespace OnHookLogger
{
	public record EventSearchRecord(string[] DeclaringTypes, EventInfo Event);
}