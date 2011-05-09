//-------------------------------------------------------------------
/*! @file BasicFallbackLogger.cs
 * @brief This file defines the static BasicFallbackLogger class that is used to emit log messages when the normal log distribution system cannot otherwise be used.  Typically this class is used within the log distribution system itself.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2007 Mosaic Systems Inc., All rights reserved. (C++ library version)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
//-------------------------------------------------------------------

namespace MosaicLib
{
	using System.Runtime.InteropServices;

	public static partial class Logging
	{
		/// <summary>
		/// This static class provides a set of robust static methods that can be used 
		/// to perform basic logging to the debugger and to a simple append only,
		/// heavily flushed text file.  This class and its members should not be used
		/// for any normal or high volumn logging application.
		/// </summary>
		/// <remarks>
		/// This logging system is only intended to be used in cases where 
		/// other primary logging and distribution services are suspect or are 
		/// otherwise unusable.
		/// 
		/// The implementation of the methods provided here are intended to be as 
		/// robust as possible, and are specifically intended to support us in 
		/// situations where static and global variables may be being destroyed
		/// while the methods are being invoked.  
		/// </remarks>

		public static class BasicFallbackLogging
		{
			/// <summary>
			/// Provide local and public access to OutputDebugString method
			/// </summary>
			/// <param name="message"></param>
			[DllImport("Kernel32.dll")]
			public static extern void OutputDebugString(string message);

			/// <summary>
			/// Provide Write or String.Format style version of OutputDebugString
			/// </summary>
			/// <param name="fmt">formated contents specifier string</param>
			/// <param name="args">set of parameters/arguments that are to be used with the formated contents specifier string</param>
			public static void OutputDebugString(string fmt, params object [] args)
			{
				OutputDebugString(Utils.Fcns.CheckedFormat(fmt, args));
			}

			/// <summary>
			/// Allows the caller to change the BasicFallbackLogger's text file to use a specific one.  Set to null to prevent BasicFallbackLogger from attempting to write to a file.
			/// </summary>
			/// <param name="path"></param>
			public static void SetFilePath(string path)
			{
				lock (logFileMutex)
				{
					logFilePath = path;
                    useFileOutput = (path != null);
				}
			}

			public static void LogError(string mesg) { Log("Error", mesg); }
			public static void LogWarning(string mesg) { Log("Warning", mesg); }
			public static void LogInfo(string mesg) { Log("Info", mesg); }

			public static void LogError(string fmt, params object [] args) { Log("Error", fmt, args); }
			public static void LogWarning(string fmt, params object [] args) { Log("Warning", fmt, args); }
			public static void LogInfo(string fmt, params object [] args) { Log("Info", fmt, args); }

			public static void Log(string type, string fmt, params object [] args)
			{
				Log(type, Utils.Fcns.CheckedFormat(fmt, args));
			}

			public static void Log(string type, string mesg)
			{
				System.DateTime localTime = System.DateTime.Now;
				string dateTimeStr = localTime.ToString("o");
				string outputStr = Utils.Fcns.CheckedFormat("{0}\t{1}\t{2}\r\n", dateTimeStr, type, mesg);

				if (useTraceOutput)
					OutputDebugString(outputStr);

				if (!useFileOutput)
					return;

				lock (logFileMutex)
				{
					try
					{
						// note: the following code is optimized for brevity and reliability, NOT for efficiency.
						using (System.IO.Stream fs = new System.IO.FileStream(logFilePath, System.IO.FileMode.Append, System.IO.FileAccess.Write, System.IO.FileShare.Read))
						using (System.IO.TextWriter tw = new System.IO.StreamWriter(fs, stringEncoder))
						{
							tw.Write(outputStr);
							tw.Flush();
							tw.Close();
							fs.Close();
						}
					}
					catch (System.Exception e)
					{
						OutputDebugString("Caught exception {0} in BasicFallbackLogging.Log while writting to file {1}", e.Message, logFilePath);
					}
				}
			}

			private static volatile bool useTraceOutput = true;
			private static volatile bool useFileOutput = true;
			private static string logFilePath = "BasicFallbackLog.txt";
			private static object logFileMutex = new object();
			private static System.Text.Encoding stringEncoder = new System.Text.UTF8Encoding();
		}
	}

	//-------------------------------------------------------------------
}

//-------------------------------------------------------------------
