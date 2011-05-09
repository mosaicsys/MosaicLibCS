//-------------------------------------------------------------------
/*! @file QueueLogger.cs
 * @brief This file defines the QueueLogger class.
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
	using System;

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
			public QueuedLogger(string name) : this (name, string.Empty) {}
			public QueuedLogger(string name, LogGate initialInstanceLogGate, bool includeFileAndLines) : this(name, string.Empty, initialInstanceLogGate, includeFileAndLines) { }
			public QueuedLogger(string name, string groupName) : this (name, groupName, LogGate.All, true) {}
			public QueuedLogger(string name, string groupName, LogGate initialInstanceLogGate, bool includeFileAndLines) : base(name, groupName, initialInstanceLogGate, includeFileAndLines)
			{
				if (dist != null)
					dist.StartQueuedMessageDelivery();
			}

			QueuedLogger(QueuedLogger rhs) : base(rhs) {}

			public override void EmitLogMessage(ref LogMessage mesg)				//!< Emits and consumes the message (mesgP will be set to null)
			{
				if (mesg != null && !loggerHasBeenShutdown)
				{
					mesg.NoteEmitted();
					dist.EnqueueMessageForDistribution(ref mesg);
				}
			}

			public override bool WaitForDistributionComplete(TimeSpan timeLimit)
			{
				if (dist == null)
					return false;

				return dist.WaitForQueuedMessageDistributionComplete(sourceInfo.ID, timeLimit);
			}

			protected override string ClassName { get { return "QueuedLogger"; } }
		}
	}

	//-------------------------------------------------------------------
}

//-------------------------------------------------------------------
