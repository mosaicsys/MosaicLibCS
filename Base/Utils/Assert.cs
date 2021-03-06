//-------------------------------------------------------------------
/*! @file Assert.cs
 *  @brief This file provides a small set of class and related definitions for classes and methods that are useful in constructing assert style operations with programmatically defined behavior.
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

using System;
using System.Runtime.InteropServices;

namespace MosaicLib.Utils
{
	/// <summary>
    /// Enum defines the behavior of the Assertion when it is triggered or produced.
    /// <para/>LogFallback = 0, Log, DebugBreakpoint, ThrowException,
    /// <para/>Assert.DefaultAssertType = AssertType.DebugBreakpoint
    /// </summary>
	public enum AssertType
	{
        /// <summary>on assert: log message to fall back log.  Behavior common to all AssertTypes.</summary>
		LogFallback = 0,

        /// <summary>on assert: log message from Assert source and to fall back log</summary>
		Log,

        /// <summary>on assert: log message to fall back log and take breakpoint if code is being run under a debugger</summary>
		DebugBreakpoint,		

        /// <summary>on assert: log message to fall back log and throw AssertException with message</summary>
        ThrowException,

        /// <summary>The use of this value has been deprecated and is no longer supported</summary>
        [Obsolete("The use of this value has been deprecated and is no longer supported (2016-05-28)")]
        FatalExit,
	};

	/// <summary>Provides a specific exception type that is thrown on Asserts with the AssertType of ThrowExecption</summary>
	public class AssertException : System.Exception
	{
        /// <summary>Constructors accepts a message and a StackFrame, from which the file and line are derived.</summary>
		public AssertException(string mesg, System.Diagnostics.StackFrame sourceFrame) : base(mesg) { this.sourceFrame = sourceFrame; }

		private System.Diagnostics.StackFrame sourceFrame;

        /// <summary>Reports the full StackFrame of the Assert related method invocation that failed and/or generated this exception</summary>
        public System.Diagnostics.StackFrame SourceFrame { get { return sourceFrame; } }
        /// <summary>Reports the file name of the original Assert related method invocation that failed and/or generated this exception</summary>
        public string File { get { return sourceFrame.GetFileName(); } }
        /// <summary>Reports the file line of the original Assert related method invocation that failed and/or generated this exception</summary>
        public int Line { get { return sourceFrame.GetFileLineNumber(); } }

        /// <summary>Returns readable/logable version of this exception</summary>
        public override string ToString() { return string.Format("Exception:{0} at file:{1}, line:{2}", Message, File, Line); }
	}

	/// <summary>
    /// This static helper class defines a set of static methods that may be used by client code to perform assertion and assertion failure reaction logic in a well defined manner
    /// </summary>
    /// <remarks>
    /// This static helper class was renamed from Assert to Asserts to remove a class/method name collision with the Assert method name in the NUnit.Framework namespace.
    /// </remarks>
	public static class Asserts
    {
        #region Standard public methods [standard variations of the normal methods that clients use from this static helper class]

        /// <summary>Logs the given condDesc condition description as an assert error message if the given cond condition is not true.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void LogIfConditionIsNotTrue(bool cond, string condDesc) { if (!cond) NoteConditionCheckFailed(condDesc, AssertType.Log, new System.Diagnostics.StackFrame(1, true)); }
        
        /// <summary>Logs the given condDesc as an assert Condition Failed error message.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void LogThatConditionCheckFailed(string condDesc) { NoteConditionCheckFailed(condDesc, AssertType.Log, new System.Diagnostics.StackFrame(1, true)); }
        
        /// <summary>Logs the given faultDesc as an Assert Fault error message</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void LogFaultOccurance(string faultDesc) { NoteFaultOccurance(faultDesc, null, AssertType.Log, new System.Diagnostics.StackFrame(1, true)); }
        
        /// <summary>Logs the given faultDesc as an Assert Fault error message with the given faultDesc fault description and the given ex exception.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void LogFaultOccurance(string faultDesc, System.Exception ex) { NoteFaultOccurance(faultDesc, ex, AssertType.Log, new System.Diagnostics.StackFrame(1, true)); }

        
        /// <summary>Takes a debugger breakpoint if the given cond condition is not true.  Also logs a Condition Failed message for the given condition description in this case.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void TakeBreakpointIfConditionIsNotTrue(bool cond, string condDesc) { if (!cond) NoteConditionCheckFailed(condDesc, AssertType.DebugBreakpoint, new System.Diagnostics.StackFrame(1, true)); }
        
        /// <summary>Takss a debugger breakpoint and logs a Condition Failed message for the given condDesc condition description</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void TakeBreakpointAfterConditionCheckFailed(string condDesc) { NoteConditionCheckFailed(condDesc, AssertType.DebugBreakpoint, new System.Diagnostics.StackFrame(1, true)); }
        
        /// <summary>Takes a debugger breakpoint and logs an Assert Fault message for the given faultDesc fault description.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void TakeBreakpointAfterFault(string faultDesc) { NoteFaultOccurance(faultDesc, null, AssertType.DebugBreakpoint, new System.Diagnostics.StackFrame(1, true)); }
        
        /// <summary>Takes a debugger breakpoint and logs an Assert Fault message for the given faultDesc fault description and ex exception.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void TakeBreakpointAfterFault(string faultDesc, System.Exception ex) { NoteFaultOccurance(faultDesc, ex, AssertType.DebugBreakpoint, new System.Diagnostics.StackFrame(1, true)); }


        /// <summary>Throws an <see cref="AssertException"/> if the given condition is not true. </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ThrowIfConditionIsNotTrue(bool cond, string condDesc) { if (!cond) NoteConditionCheckFailed(condDesc, AssertType.ThrowException, new System.Diagnostics.StackFrame(1, true)); }
        
        /// <summary>Throws an <see cref="AssertException"/>. </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ThrowAfterFault(string faultDesc) { NoteFaultOccurance(faultDesc, null, AssertType.ThrowException, new System.Diagnostics.StackFrame(1, true)); }
        
        /// <summary>Throws an <see cref="AssertException"/> with the given ex as the inner exception. </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ThrowAfterFault(string faultDesc, System.Exception ex) { NoteFaultOccurance(faultDesc, ex, AssertType.ThrowException, new System.Diagnostics.StackFrame(1, true)); }

        /// <summary>Defines the default assert type for the following non-typed assert methods.  defaults to AssertType.DebugBreakpoint</summary>
        public static AssertType DefaultAssertType = AssertType.DebugBreakpoint;

        
        /// <summary>Asserts that the given cond condition is true.  If not then it uses the DefaultAssertType to define what action is taken.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void CheckIfConditionIsNotTrue(bool cond, string condDesc) { if (!cond) NoteConditionCheckFailed(condDesc, DefaultAssertType, new System.Diagnostics.StackFrame(1, true)); }
        
        /// <summary>Asserts that the described condition (condDesc) test has failed.  Uses the DefaultAssertType to define what action is taken.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void NoteConditionCheckFailed(string condDesc) { NoteConditionCheckFailed(condDesc, DefaultAssertType, new System.Diagnostics.StackFrame(1, true)); }
        
        /// <summary>Asserts that a described fault (faultDesc) has occurred.  Uses the DefaultAssertType to define what action is taken.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void NoteFaultOccurance(string faultDesc) { NoteFaultOccurance(faultDesc, null, DefaultAssertType, new System.Diagnostics.StackFrame(1, true)); }
        
        /// <summary>Asserts that a described fault (faultDesc) and exception has occurred.  Uses the DefaultAssertType to define what action is taken.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void NoteFaultOccurance(string faultDesc, System.Exception ex) { NoteFaultOccurance(faultDesc, ex, DefaultAssertType, new System.Diagnostics.StackFrame(1, true)); }

        
        /// <summary>Asserts that the given cond condition is true.  Action taken when the condition is not true is determined by the given assertType value.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void CheckIfConditionIsNotTrue(bool cond, string condDesc, AssertType assertType) { if (!cond) NoteConditionCheckFailed(condDesc, assertType, new System.Diagnostics.StackFrame(1, true)); }
        
        /// <summary>Reports that the given condDesc described condition test failed.  Action taken when the condition is not true is determined by the given assertType value.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void NoteConditionCheckFailed(string condDesc, AssertType assertType) { NoteConditionCheckFailed(condDesc, assertType, new System.Diagnostics.StackFrame(1, true)); }

        /// <summary>Reports that the faultDesc described fault has occurred.  Action taken when the condition is not true is determined by the given assertType value.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void NoteFaultOccurance(string faultDesc, AssertType assertType) { NoteFaultOccurance(faultDesc, null, assertType, new System.Diagnostics.StackFrame(1, true)); }

        /// <summary>Reports that the faultDesc described fault and the ex Exception has occurred.  Action taken when the condition is not true is determined by the given assertType value.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void NoteFaultOccurance(string faultDesc, System.Exception ex, AssertType assertType) { NoteFaultOccurance(faultDesc, ex, assertType, new System.Diagnostics.StackFrame(1, true)); }

		#endregion

        #region Public adapter/formatter methods

        /// <summary>Common implementation method for Noting that a condition test failed.</summary>
        public static void NoteConditionCheckFailed(string condDesc, AssertType assertType, System.Diagnostics.StackFrame sourceFrame)
		{
			AssertCommon(Fcns.CheckedFormat("AssertCondition:[{0}] Failed", condDesc), assertType, sourceFrame);
		}

        /// <summary>Common implementation method for Noting that Fault has occurred, with or without a related System.Exception ex.</summary>
        public static void NoteFaultOccurance(string faultDesc, System.Exception ex, AssertType assertType, System.Diagnostics.StackFrame sourceFrame)
		{
			if (ex == null)
				AssertCommon(Fcns.CheckedFormat("AssertFault:{0}", faultDesc), assertType, sourceFrame);
			else
				AssertCommon(Fcns.CheckedFormat("AssertFault:{0} with exception:{1}", faultDesc, ex.Message), assertType, sourceFrame);
		}

        /// <summary>Defines the default Logging.MesgType used for logging asserts into log distribution (either directly or via QueuedLogger.  defaults to Logging.MesgType.Error</summary>
        public static Logging.MesgType DefaultAssertLoggingMesgType = Logging.MesgType.Error;

        /// <summary>Set this property to true if you would like non-log only assertions (such as DebugBreakpoint) to take a debug breakpoint.  defaults to false</summary>
        public static bool EnableAssertDebugBreakpoints { get; set; }

        /// <summary>Set this property to true if you would like Asserts to generate BasicFallbackLogging output.  defaults to false</summary>
        public static bool EnableBasicFallbackLogging { get; set; }

		#endregion

		#region Private methods

		private static Logging.ILogger assertLogger = null;
        private static Logging.ILogger queuedAssertLogger = null;

		/// <summary> This is the inner-most implementation method for the Assert helper class.  It implements all of the assertType specific behavior for all assertions that get triggered.</summary>
		private static void AssertCommon(string mesg, AssertType assertType, System.Diagnostics.StackFrame sourceFrame)
		{
			// always log all triggered asserts to the BasicFallbackLog

			string logStr = Fcns.CheckedFormat("{0} at file:'{1}', line:{2}", mesg, sourceFrame.GetFileName(), sourceFrame.GetFileLineNumber());

            if (assertType != AssertType.Log)
            {
                if (EnableBasicFallbackLogging)
                    Logging.BasicFallbackLogging.LogError(logStr);

                if (queuedAssertLogger == null)       // in an MT world we might create a few of these simultaneously.  This is not a problem as the distribution engine supports such a construct so locking is not required here in order to get valid behavior.
                    queuedAssertLogger = new Logging.QueuedLogger("MosaicLib.Utils.Assert");

                queuedAssertLogger.Emitter(DefaultAssertLoggingMesgType).Emit(logStr);
            }

			bool ignoreFault = false;		// intended to be used by debug user to ignore such asserts on a case by case basis

			if (assertType == AssertType.Log)
			{
                if (assertLogger == null)       // in an MT world we might create a few of these simultaneously.  This is not a problem as the distribution engine supports such a construct so locking is not required here in order to get valid behavior.
                    assertLogger = new Logging.Logger("MosaicLib.Utils.Assert");

                assertLogger.Emitter(DefaultAssertLoggingMesgType).Emit(logStr);

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
                // the remaining types always trigger a breakpoint if a debugger is attached and the hosting environment has set the EnabledAssertDebugBreakpoints flag
                if (System.Diagnostics.Debugger.IsAttached && EnableAssertDebugBreakpoints)
					System.Diagnostics.Debugger.Break();
			}
		}

		#endregion
	}
}

//-------------------------------------------------------------------
