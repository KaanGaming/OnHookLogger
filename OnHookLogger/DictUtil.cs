using System;
using System.Collections.Generic;

namespace OnHookLogger
{
	public static class DictUtil
	{
		public static T2? GetSafe<T1, T2>(this Dictionary<T1, T2> dict, T1 key) where T2 : struct
		{
			return dict.ContainsKey(key) ? dict[key] : null;
		}
	}
}