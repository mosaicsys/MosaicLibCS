//-------------------------------------------------------------------
/*! @file Assert.cs
 *  @brief This file provides a small set of class and related definitions for classes and methods that are useful in constructing assert style operations with programatically defined behavior.
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

namespace MosaicLib.Utils
{
	using System.Runtime.InteropServices;

	/// <summary>Enum defines the behavior of the Assertion when it is triggered or produced</summary>
	public enum AssertType
	{
		LogFallback = 0,		/// <summary>on assert: log message to Fallback log.  Behavior common to all AssertTypes.</summary>
		Log,					/// <summary>on assert: log message from Assert source and to Fallback log</summary>
		DebugBreakpoint,		/// <summary>on assert: log message to Fallback log and take breakpoint if code is being run under a debugger</summary>
		ThrowException,		    /// <summary>on assert: log message to Fallback log and throw AssertException with message</summary>
		FatalExit,			    /// <summary>on assert: log message to Fallback log and cause program to exit abortively</summary>
	};

	/// <summary>Provides a specific exeception type that is thrown on Asserts with the AssertType of ThrowExecption</summary>
	public class AssertException : System.Exception
	{
		public AssertException(string mesg, System.Diagnostics.StackFrame sourceFrame) : base(mesg) { this.sourceFrame = sourceFrame; }

		private System.Diagnostics.StackFrame sourceFrame;

		public System.Diagnostics.StackFrame SourceFrame { get { return sourceFrame; } }
		public string File { get { return sourceFrame.GetFileName(); } }
		public int Line { get { return sourceFrame.GetFileLineNumber(); } }

		public override string ToString() { return string.Format("Exception:{0} at file:{1}, line:{2}", Message, File, Line); }
	}

	/// <summary>This static helper class defines a set of static methods that may be used by client code to perform assertion and assertaion failure reaction logic in a well defined manner</summary>
	public static class Assert
	{
		#region Standard public methods

		// the following are the standard variations of the normal methods that clients use from this static helper class
		public static void LogCondition(bool cond, string condDesc) { if (!cond) NoteConditionFailed(condDesc, AssertType.Log, new System.Diagnostics.StackFrame(1, true)); }
		public static void LogConditionFailed(string condDesc) { NoteConditionFailed(condDesc, AssertType.Log, new System.Diagnostics.StackFrame(1, true)); }
		public static void LogFault(string faultDesc) { NoteFault(faultDesc, null, AssertType.Log, new System.Diagnostics.StackFrame(1, true)); }
		public static void LogFault(string faultDesc, System.Exception ex) { NoteFault(faultDesc, ex, AssertType.Log, new System.Diagnostics.StackFrame(1, true)); }

		public static void BreakpointCondition(bool cond, string condDesc) { if (!cond) NoteConditionFailed(condDesc, AssertType.DebugBreakpoint, new System.Diagnostics.StackFrame(1, true)); }
		public static void BreakpointConditionFailed(string condDesc) { NoteConditionFailed(condDesc, AssertType.DebugBreakpoint, new System.Diagnostics.StackFrame(1, true)); }
		public static void BreakpointFault(string faultDesc) { NoteFault(faultDesc, null, AssertType.DebugBreakpoint, new System.Diagnostics.StackFrame(1, true)); }
		public static void BreakpointFault(string faultDesc, System.Exception ex) { NoteFault(faultDesc, ex, AssertType.DebugBreakpoint, new System.Diagnostics.StackFrame(1, true)); }

		public static void ThrowCondition(bool cond, string condDesc) { if (!cond) NoteConditionFailed(condDesc, AssertType.ThrowException, new System.Diagnostics.StackFrame(1, true)); }
		public static void ThrowFault(string faultDesc) { NoteFault(faultDesc, null, AssertType.ThrowException, new System.Diagnostics.StackFrame(1, true)); }
		public static void ThrowFault(string faultDesc, System.Exception ex) { NoteFault(faultDesc, ex, AssertType.ThrowException, new System.Diagnostics.StackFrame(1, true)); }

		public static AssertType DefaultAssertType = AssertType.DebugBreakpoint;

		public static void Condition(bool cond, string condDesc) { if (!cond) NoteConditionFailed(condDesc, DefaultAssertType, new System.Diagnostics.StackFrame(1, true)); }
		public static void ConditionFailed(string condDesc) { NoteConditionFailed(condDesc, DefaultAssertType, new System.Diagnostics.StackFrame(1, true)); }
		public static void Fault(string faultDesc) { NoteFault(faultDesc, null, DefaultAssertType, new System.Diagnostics.StackFrame(1, true)); }
		public static void Fault(string faultDesc, System.Exception ex) { NoteFault(faultDesc, ex, DefaultAssertType, new System.Diagnostics.StackFrame(1, true)); }

		public static void Condition(bool cond, string condDesc, AssertType assertType) { if (!cond) NoteConditionFailed(condDesc, assertType, new System.Diagnostics.StackFrame(1, true)); }
		public static void ConditionFailed(string condDesc, AssertType assertType) { NoteConditionFailed(condDesc, assertType, new System.Diagnostics.StackFrame(1, true)); }
		public static void Fault(string faultDesc, AssertType assertType) { NoteFault(faultDesc, null, assertType, new System.Diagnostics.StackFrame(1, true)); }
		public static void Fault(string faultDesc, System.Exception ex, AssertType assertType) { NoteFault(faultDesc, ex, assertType, new System.Diagnostics.StackFrame(1, true)); }

		#endregion

		#region Public adapter/formatter methods

		// the following methods convert the standard invocation versions into arguments that can be given to the AssertCommon method
		public static void TestCondition(bool cond, string condDesc, AssertType assertType, System.Diagnostics.StackFrame sourceFrame)
		{
			if (!cond)
				NoteConditionFailed(condDesc, assertType, sourceFrame);
		}

		public static void NoteConditionFailed(string condDesc, AssertType assertType, System.Diagnostics.StackFrame sourceFrame)
		{
			AssertCommon(Fcns.CheckedFormat("AssertCondition:[{0}] Failed", condDesc), assertType, sourceFrame);
		}
		public static void NoteFault(string faultDesc, System.Exception ex, AssertType assertType, System.Diagnostics.StackFrame sourceFrame)
		{
			if (ex == null)
				AssertCommon(Fcns.CheckedFormat("AssertFault:{0}", faultDesc), assertType, sourceFrame);
			else
				AssertCommon(Fcns.CheckedFormat("AssertFault:{0} with exception:{1}", faultDesc, ex.Message), assertType, sourceFrame);
		}

		#endregion

		#region Private methods

		private static Logging.ILogger assertLogger = new Logging.Logger("MosaicLib.Utils.Assert");

		/// <summary> This is the inner-most implementation method for the Assert helper class.  It implements all of the assertType specific behavior for all assertions that get triggered.</summary>
		private static void AssertCommon(string mesg, AssertType assertType, System.Diagnostics.StackFrame sourceFrame)
		{
			// always log all triggered asserts to the BasicFallbackLog

			string logStr = Fcns.CheckedFormat("{0} at file:'{1}', line:{2}", mesg, sourceFrame.GetFileName(), sourceFrame.GetFileLineNumber());

			if (assertType != AssertType.Log)
				Logging.BasicFallbackLogging.LogError(logStr);

			bool ignoreFault = false;		// intended to be used by debug user to ignore such asserts on a case by case basis

			if (assertType == AssertType.Log)
			{
				assertLogger.Warning.Emit(mesg);

				return;
			}
			else if (assertType == AssertType.LogFallback)
			{
				return;	// already done
			}
			else if (assertType == AssertType.ThrowException)
			{
				if (!ignoreFault)
					throw new AssertException(mesg, sourceFrame);

				return;
			}

			if (!ignoreFault)
			{
				// the remaining types allways trigger a breakpoint
				if (System.Diagnostics.Debugger.IsAttached)
					System.Diagnostics.Debugger.Break();

				// finally if the type is a FatalExit then call the Kernel32.dll FatalExit entry point
				if (assertType == AssertType.FatalExit)
					FatalExit(-1);
			}
		}

		[DllImport("Kernel32.dll")]
		private static extern void FatalExit(int exitCode);

		#endregion
	}
}

//-------------------------------------------------------------------
