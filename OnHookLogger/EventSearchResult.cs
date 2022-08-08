using System.Reflection;

namespace OnHookLogger
{
	public record EventSearchResult(string[] DeclaringTypes, EventInfo Event)
	{
		public override string ToString()
		{
			return $"On.{string.Join(".", DeclaringTypes)}.{Event.Name}";
		}
	}
}