//-------------------------------------------------------------------
/*! @file QueueLogger.cs
 *  @brief This file defines the QueueLogger class.
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

namespace MosaicLib
{
	public static partial class Logging
	{
		//-------------------------------------------------------------------
		/// <summary>
		/// This class provides a version of the Logger class that emits its messages into
		/// the LogDistribution mesg queue rather than directly to the logger's distribution group.
		/// This is useful for clients that must not block when calling into the logging system (such
		/// as in components that may be used under the logging system).
		/// </summary>
		/// <remarks>
		/// This is essential for certain components that need to provide logging but may be used
		/// within the logging system and as such need to make use of the mesgQueue to prevent
		/// deadlock and related issues.  This type of logger is also usefull for any message
		/// source that can emit large bursts of messages.
		/// 
		/// Please note that messages that the order of messages are emitted to the distribution 
		/// message queue will only be preseved relative to the order of other messages that are so emitted.
		/// Messages from non-queued loggers will generally be passed into the distribution system
		/// ahead of messages from the mesg queue.  Relative timeing of message recording will not
		/// be preserved between queued loggers and non-queued loggers.
		/// </remarks>
		public class QueuedLogger : LoggerBase
		{
            /// <summary>Constructor.  Uses given logger name.  Uses default group name, LogGate.All and enables instance trace logging</summary>
            /// <param name="name">Provides the LoggerName (source ID) to use for this logger.</param>
            public QueuedLogger(string name) 
                : this (name, string.Empty) 
            {}

            /// <summary>Constructor.  Uses given logger name, and initialInstanceLogGate.  Enables instance trace logging</summary>
            /// <param name="name">Provides the LoggerName (source ID) to use for this logger.</param>
            /// <param name="initialInstanceLogGate">Defines the initial instance group gate that may be more restrictive than the gate assigned to the group or the logger through the distribution system.</param>
			public QueuedLogger(string name, LogGate initialInstanceLogGate) 
                : this(name, string.Empty, initialInstanceLogGate) 
            { }

            /// <summary>Constructor.  Uses given logger name, and group name.  Use LogGate.All and enables instance trace logging</summary>
            /// <param name="name">Provides the LoggerName (source ID) to use for this logger.</param>
            /// <param name="groupName">Provides the GroupName that this logger name will be assigned/moved to</param>
			public QueuedLogger(string name, string groupName) 
                : this (name, groupName, LogGate.All) 
            {}

            /// <summary>Detailed Constructor.  Uses given logger name, group name, and initialInstanceLogGate.  Use default group name and enables instance trace logging</summary>
            /// <param name="name">Provides the LoggerName (source ID) to use for this logger.</param>
            /// <param name="groupName">Provides the GroupName that this logger name will be assigned/moved to</param>
            /// <param name="initialInstanceLogGate">Defines the initial instance group gate that may be more restrictive than the gate assigned to the group or the logger through the distribution system.</param>
            /// <param name="callerProvidedLMD">can be used to define the ILogMessageDistribution instance that this object will be used with</param>
            public QueuedLogger(string name, string groupName, LogGate initialInstanceLogGate, ILogMessageDistribution callerProvidedLMD = null) 
                : base(name, groupName, initialInstanceLogGate, callerProvidedLMD: callerProvidedLMD)
			{
                dist4q = callerProvidedLMD ?? Logging.LogMessageDistribution.Instance;

                if (dist4q == null)
                    Utils.Asserts.TakeBreakpointAfterFault(ClassName + ": LogMessageDistribution is null");

                if (dist4q != null)
                    dist4q.StartQueuedMessageDeliveryIfNeeded();
			}

            /// <summary>Copy constructor.</summary>
            /// <param name="rhs">Gives the Logger instance to make a copy from.</param>
			QueuedLogger(QueuedLogger rhs) 
                : base(rhs) 
            {
                dist4q = rhs.dist4q;
            }

            /// <summary>Emits and consumes the message (mesg will be set to null)</summary>
            public override void EmitLogMessage(ref LogMessage mesg)	
			{
                if (mesg != null && !loggerHasBeenShutdown && dist4q != null)
				{
					mesg.NoteEmitted();
                    dist4q.EnqueueMessageForDistribution(ref mesg);
				}
			}

            /// <summary>Waits for last message emitted by this logger to have been distributed and processed</summary>
            /// <returns>true if distribution of the last message emitted here completed within the given time limit, false otherwise.</returns>
            public override bool WaitForDistributionComplete(TimeSpan timeLimit)
			{
                if (dist4q == null)
					return false;

                return dist4q.WaitForQueuedMessageDistributionComplete(sourceInfo.ID, timeLimit);
			}

            /// <summary>Defines the ClassName value that will be used by the LoggerBase when generating trace messages (if enabled).</summary>
            protected override string ClassName { get { return "QueuedLogger"; } }

            protected ILogMessageDistributionForQueuedLoggers dist4q;
		}
	}

	//-------------------------------------------------------------------
}

//-------------------------------------------------------------------
