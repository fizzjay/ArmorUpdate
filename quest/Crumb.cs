using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ArmorRestoredQuest
{
	// Token: 0x02000003 RID: 3
	internal static class Crumb
	{
		// Token: 0x06000008 RID: 8
		[DllImport("liblog.so", EntryPoint = "__android_log_write")]
		private static extern int LogWrite(int prio, [MarshalAs(UnmanagedType.LPUTF8Str)] string tag, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

		// Token: 0x06000009 RID: 9
		[DllImport("libc.so", EntryPoint = "__cxa_atexit")]
		private static extern int AtExitRegister(IntPtr fn, IntPtr arg, IntPtr dso);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void ExitCallback(IntPtr arg);

		// libc retains this callback: keep the delegate alive for the process lifetime.
		private static readonly ExitCallback NativeExitCallback = AtExit;
		private static bool watchingExit;

		// Token: 0x0600000A RID: 10 RVA: 0x0000223F File Offset: 0x0000043F
		internal static void At(string what)
		{
			Crumb.Write(what);
		}

		// Token: 0x0600000B RID: 11 RVA: 0x00002247 File Offset: 0x00000447
		internal static void First(string what)
		{
			if (Crumb.firsts.Add(what))
			{
				Crumb.Write("first " + what);
			}
		}

		// Token: 0x0600000C RID: 12 RVA: 0x00002268 File Offset: 0x00000468
		internal static void WatchExit()
		{
			if (watchingExit) return;
			watchingExit = true;
			try
			{
				AppDomain.CurrentDomain.ProcessExit += delegate(object sender, EventArgs args)
				{
					Crumb.exiting = true;
					Crumb.Write("managed ProcessExit");
				};
				int num = Crumb.AtExitRegister(Marshal.GetFunctionPointerForDelegate(NativeExitCallback), IntPtr.Zero, IntPtr.Zero);
				Crumb.Write("exit watch in (atexit " + ((num == 0) ? "registered" : ("failed " + num.ToString())) + ")");
			}
			catch (Exception ex)
			{
				Crumb.Write("exit watch failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}

		// Token: 0x0600000D RID: 13 RVA: 0x00002320 File Offset: 0x00000520
		private static void AtExit(IntPtr arg)
		{
			if (Crumb.exiting)
			{
				return;
			}
			Crumb.Write("native exit(): the last crumb above is the call that ended the game");
		}

		// Token: 0x0600000E RID: 14 RVA: 0x00002334 File Offset: 0x00000534
		private static void Write(string what)
		{
			try
			{
				StringBuilder stringBuilder = new StringBuilder(what.Length + 16);
				stringBuilder.Append('#').Append(Interlocked.Increment(ref Crumb.seq)).Append(' ').Append(what);
				Crumb.LogWrite(4, "ArmorQuest", stringBuilder.ToString());
			}
			catch
			{
			}
		}

		// Token: 0x0400000A RID: 10
		private const string Tag = "ArmorQuest";

		// Token: 0x0400000B RID: 11
		private static readonly HashSet<string> firsts = new HashSet<string>();

		// Token: 0x0400000C RID: 12
		private static int seq;

		// Token: 0x0400000D RID: 13
		private static bool exiting;
	}
}
