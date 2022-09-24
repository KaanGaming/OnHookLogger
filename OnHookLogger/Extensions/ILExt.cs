using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace OnHookLogger.Extensions
{
	public static class ILExt
	{
		public static EmitObjResult EmitObject(this ILGenerator il, object obj, Type type)
		{
			GCHandle ptrHandle = GCHandle.Alloc(obj, GCHandleType.Pinned);

			IntPtr ptr = GCHandle.ToIntPtr(ptrHandle);

			if (IntPtr.Size == 4) // if processor is 32bit
				il.Emit(OpCodes.Ldc_I4, ptr.ToInt32());
			else // if processor is 64bit
				il.Emit(OpCodes.Ldc_I8, ptr.ToInt64());
			il.Emit(OpCodes.Conv_I); /* convert the number to native int anyways
			                                  ldobj will use a native int instead of an int32 or int64 */

			il.Emit(OpCodes.Ldobj, type);

			return new EmitObjResult(ptrHandle, il);
		}

		public static void EmitCNull(this ILGenerator il)
		{
			il.Emit(OpCodes.Ldnull);
			il.Emit(OpCodes.Ceq);
		}

		public static void EmitIf(this ILGenerator il, CondType cond, Action emitters, Label label)
		{
			OpCode condCode = cond switch
			{
				CondType.False => OpCodes.Brtrue,
				CondType.True => OpCodes.Brfalse,
				CondType.Equals => OpCodes.Bne_Un,
				CondType.NotEquals => OpCodes.Beq,
				CondType.GreaterOrEqual => OpCodes.Blt,
				CondType.GreaterThan => OpCodes.Ble,
				CondType.LesserOrEqual => OpCodes.Bgt,
				CondType.LesserThan => OpCodes.Bge,
				_ => OpCodes.Br
			};

			il.Emit(condCode, label);
			emitters();
			il.MarkLabel(label);
		}

		public class EmitObjResult
		{
			internal EmitObjResult(GCHandle handle, ILGenerator il)
			{
				this.handle = handle;
				_il = il;
			}

			public GCHandle handle;
			private ILGenerator _il;

			public GCHandle ToReference()
			{
				_il.Emit(OpCodes.Box);
				return handle;
			}

			public static implicit operator GCHandle(EmitObjResult obj)
			{
				return obj.handle;
			}
		}
	}

	public enum CondType
	{
		False,
		True,
		Equals,
		NotEquals,
		GreaterOrEqual,
		GreaterThan,
		LesserOrEqual,
		LesserThan
	}
}
