//-------------------------------------------------------------------
/*! @file BasicFallbackLogger.cs
 *  @brief This file defines the static BasicFallbackLogger class that is used to emit log messages when the normal log distribution system cannot otherwise be used.  Typically this class is used within the log distribution system itself.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
 * Copyright (c) 2007 Mosaic Systems Inc.  (C++ library version)
 * All rights reserved.
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

using System.Runtime.InteropServices;

namespace MosaicLib
{
    /// <summary>
    /// The contents of this file extend the Logging class/namespace.
    /// </summary>
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
			public static void SetFilePath(string path)
			{
				lock (logFileMutex)
				{
					logFilePath = path;
                    useFileOutput = (path != null);
				}
			}

            /// <summary>Logs the given mesg string using Type "Error"</summary>
 			public static void LogError(string mesg) { Log("Error", mesg); }
            /// <summary>Logs the given mesg string using Type "Warning"</summary>
            public static void LogWarning(string mesg) { Log("Warning", mesg); }
            /// <summary>Logs the given mesg string using Type "Info"</summary>
            public static void LogInfo(string mesg) { Log("Info", mesg); }

            /// <summary>Formats the given arguments using CheckedFormat and Logs the resulting string using Type "Error"</summary>
            public static void LogError(string fmt, params object[] args) { Log("Error", fmt, args); }
            /// <summary>Formats the given arguments using CheckedFormat and Logs the resulting string using Type "Warning"</summary>
            public static void LogWarning(string fmt, params object[] args) { Log("Warning", fmt, args); }
            /// <summary>Formats the given arguments using CheckedFormat and Logs the resulting string using Type "Info"</summary>
            public static void LogInfo(string fmt, params object[] args) { Log("Info", fmt, args); }

            /// <summary>Formats the given arguments using CheckedFormat and Logs the resulting string using using the given type string</summary>
            public static void Log(string type, string fmt, params object[] args)
			{
				Log(type, Utils.Fcns.CheckedFormat(fmt, args));
			}

            /// <summary>Appends a text line containing the current date, and the given type and message strings all seperated by tabs, to the current fallback logging file.</summary>
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
					catch (System.Exception ex)
					{
						OutputDebugString("Caught exception {0} in BasicFallbackLogging.Log while writting to file {1}", ex.Message, logFilePath);
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
