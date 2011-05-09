//-------------------------------------------------------------------
/*! @file AutoEchoWrapper.cs
 * @brief This file defines the AutoEchoWrapper part that accepts a port and writes back to it everything that is received from it while it is connected.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
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

namespace MosaicLib.SerialIO
{
	//-------------------------------------------------------------------
	using System;
	using MosaicLib.Modular.Action;
	using MosaicLib.Modular.Part;
	using MosaicLib.Time;
	using MosaicLib.Utils;

	//-------------------------------------------------------------------

	public class AutoEchoWrapper : SimpleActivePartBase
	{
		//----------------------------------------
		public AutoEchoWrapper(IPort wrappedPort) : this(wrappedPort.PartID + ".aew", wrappedPort) { }

		public AutoEchoWrapper(string wrapperName, IPort wrappedPort)
			: base(wrapperName, "AutoEchoWrapper")
		{
			port = wrappedPort;

			portWrActionParam.Buffer = portRdActionParam.Buffer;

			portRdAction = port.CreateReadAction(portRdActionParam);
			portWrAction = port.CreateWriteAction(portWrActionParam);

			portRdAction.NotifyOnComplete.OnNotify += this.ReadActionComplete;
			portWrAction.NotifyOnComplete.OnNotify += this.WriteActionComplete;

			// I Want the BaseStateNotifier to act as both a IGuardedNotificationObject and an ISequencedRefObjectSource so that
			//	can signal on its state change AND use an observer to look at it cheaply.

			portBaseStateObserver = new SequencedRefObjectSourceObserver<IBaseState, int>(port.BaseStateNotifier);

			portBaseStateObserver.Update();

			SetBaseState(UseState.Initial, ConnState.Initial, "CTOR", true);
		}

		//----------------------------------------
		#region private fields and methods

		IPort port = null;

		ReadActionParam portRdActionParam = new ReadActionParam(1024);
		WriteActionParam portWrActionParam = new WriteActionParam();

		IReadAction portRdAction = null;
		IWriteAction portWrAction = null;

		void ReadActionComplete() { Notify(); }
		void WriteActionComplete() { Notify(); }

		ISequencedRefObjectSourceObserver<IBaseState, int> portBaseStateObserver = null;

		#endregion

		//----------------------------------------
		#region SimpleActivePart overridden methods

		protected override void DisposeCalledPassdown(MosaicLib.Utils.DisposableBase.DisposeType disposeType)
		{
			if (disposeType == DisposeType.CalledExplicitly)
				MosaicLib.Utils.Fcns.DisposeOfObject(ref port);

			base.DisposeCalledPassdown(disposeType);
		}

		protected override string PerformGoOnlineAction(bool andInitialize)
		{
            IBasicAction iba = (IBasicAction) port.CreateGoOnlineAction(andInitialize);
			string rc = iba.Run();
			PerformMainLoopService();
			return rc;
		}

		protected override string PerformGoOfflineAction()
		{
            IBasicAction iba = (IBasicAction) port.CreateGoOfflineAction();
			string rc = iba.Run();
			PerformMainLoopService();
			return rc;
		}

		protected override void PerformMainLoopService()
		{
			System.Threading.Thread.Sleep(1);

			IBaseState portBaseState = portBaseStateObserver.Object;

			if (portBaseStateObserver.IsUpdateNeeded)
			{
				portBaseStateObserver.Update();

				IBaseState updatedBaseState = portBaseStateObserver.Object;

				if (updatedBaseState.ConnState != BaseState.ConnState)
					SetBaseState(updatedBaseState.ConnState, "Port ConnState changed", true);

				portBaseState = updatedBaseState;
			}

			if (portBaseState.IsConnected)
			{
				if (portRdAction.ActionState.IsComplete && portRdActionParam.ActionResultEnum != ActionResultEnum.None)
				{
					if (portRdActionParam.BytesRead > 0 && portWrAction.ActionState.CanStart)
					{
						portRdActionParam.ActionResultEnum = ActionResultEnum.None;
						portWrActionParam.BytesToWrite = portRdActionParam.BytesRead;
						portWrAction.Start();
					}
				}
				else if (portWrAction.ActionState.IsComplete && portWrActionParam.ActionResultEnum != ActionResultEnum.None)
				{
					portWrActionParam.ActionResultEnum = ActionResultEnum.None;
					portRdAction.Start();
				}
				else if (portRdAction.ActionState.CanStart && portWrAction.ActionState.CanStart)
				{
					portWrActionParam.Reset();
					portRdAction.Start();
				}				
			}
		}

		#endregion

		//----------------------------------------
	}

	//-------------------------------------------------------------------
}

//-------------------------------------------------------------------
