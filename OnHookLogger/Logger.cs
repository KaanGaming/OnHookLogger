using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OnHookLogger
{
	public class Logger : IDisposable
	{
		public Logger(string fileLoc)
		{
			_writer = File.CreateText(fileLoc);
			_writer.AutoFlush = true;
		}

		private StreamWriter _writer;

		public void Log(OnHookEvent ev)
		{
			_writer.WriteLine(ev);
		}

		public void Close()
		{
			_writer.Close();
		}

		public void Dispose()
		{
			_writer.Dispose();
		}
	}
}
