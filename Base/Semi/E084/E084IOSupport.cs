//-------------------------------------------------------------------
/*! @file E084IOSupport.cs
 *  @brief This file defines the E084IOSupport interfaces and structures.
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2010 Mosaic Systems Inc.
 * Copyright (c) 2006 Mosaic Systems Inc.  (C++ library version(s) Iface_E084PassiveIO.h and Iface_E084ActiveIO.h)
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

using MosaicLib.Utils;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Part;

namespace MosaicLib.Semi.E084.IOSupport
{
    #region PassiveToActive interface items

    /// <summary>Object contains the full set of information that defines the current state of an E084 Passive side IO interface.</summary>
    public struct PassiveIOState : IEquatable<PassiveIOState>
    {
        /// <summary>PassiveToActivePinsState setpoint</summary>
        public PassiveToActivePinsState outputs;

        /// <summary>PassiveToActivePinsState as last successfully updated by interface</summary>
        public PassiveToActivePinsState outputsReadback;

        /// <summary>True if there is an Output update that is (still) pending.  Once an output update has been posted this will remain true until the readback matches the output setpoint and there are no outstanding output update commands that are running.</summary>
        public bool OutputIsPending { get; set; }

        /// <summary>Gives the most recent state of the input ActiveToPassivePinsState</summary>
        public ActiveToPassivePinsState inputs;

        /// <summary>True if the inputs are a valid representation of a recent pin state as reported by the actual hardware interface.</summary>
        public bool InputsAreValid { get; set; }

        /// <summary>Returns true if the contents of this and the given rhs object are identical</summary>
        public bool Equals(PassiveIOState rhs)
        {
            return (outputs.Equals(rhs.outputs)
                    && outputsReadback.Equals(rhs.outputsReadback)
                    && OutputIsPending == rhs.OutputIsPending
                    && inputs.Equals(rhs.inputs)
                    && InputsAreValid == rhs.InputsAreValid
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(PassiveIOState rhs)
        {
            return Equals(rhs);
        }
    }

    /// <summary>Interface defines a SetOutputsAction for a IE084PassiveIOSupport object</summary>
    public interface IPassiveIOSetOutputsAction : IBasicAction, IClientFacetWithParam<PassiveToActivePinsState> { }

    /// <summary>Interface defines the set of properties and methods that are supported by all E084 Passive Outputs objects.</summary>
    public interface IE084PassiveIOSupport
	{
        /// <summary>Returns a reference to the last published IOState from the part</summary>
        PassiveIOState IOState { get; }

        /// <summary>Property gives client access to the part's Guarded Notification Object for the part's IOState property.</summary>
        INotificationObject<PassiveIOState> StateNotifier { get; }

        /// <summary>Creates an Action object that can be used to set the PassiveToActive outputs to the given pinState value.</summary>
        IPassiveIOSetOutputsAction CreateSetOutputsAction(PassiveToActivePinsState pinsState);
    }

    #endregion

    #region ActiveToPassive interface items

    /// <summary>Object contains the full set of information that defines the current state of an E084 Passive side IO interface.</summary>
    public struct ActiveIOState : IEquatable<ActiveIOState>
    {
        /// <summary>ActiveToPassivePinsState setpoint</summary>
        public ActiveToPassivePinsState outputs;

        /// <summary>ActiveToPassivePinsState as last successfully updated by interface</summary>
        public ActiveToPassivePinsState outputsReadback;

        /// <summary>True if there is an Output update that is (still) pending.  Once an output update has been posted this will remain true until the readback matches the output setpoint and there are no outstanding output update commands that are running.</summary>
        public bool OutputIsPending { get; set; }

        /// <summary>Gives the most recent state of the input PassiveToActivePinsState</summary>
        public PassiveToActivePinsState inputs;

        /// <summary>True if the inputs are a valid representation of a recent pin state as reported by the actual hardware interface.</summary>
        public bool InputsAreValid { get; set; }

        /// <summary>Returns true if the contents of this and the given rhs object are identical</summary>
        public bool Equals(ActiveIOState rhs)
        {
            return (outputs.Equals(rhs.outputs)
                    && outputsReadback.Equals(rhs.outputsReadback)
                    && OutputIsPending == rhs.OutputIsPending
                    && inputs.Equals(rhs.inputs)
                    && InputsAreValid == rhs.InputsAreValid
                    );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(ActiveIOState rhs)
        {
            return Equals(rhs);
        }
    }

    /// <summary>Interface defines a SetOutputsAction for a IE084PassiveIOSupport object</summary>
    public interface IActiveIOSetOutputsAction : IBasicAction, IClientFacetWithParam<ActiveToPassivePinsState> { }

    /// <summary>Interface defines the set of properties and methods that are supported by all E084 Passive Outputs objects.</summary>
    public interface IE084ActiveIOSupport
    {
        /// <summary>Returns a reference to the last published IOState from the part</summary>
        ActiveIOState IOState { get; }

        /// <summary>Property gives client access to the part's Guarded Notification Object for the part's IOState property.</summary>
        INotificationObject<ActiveIOState> StateNotifier { get; }

        /// <summary>Creates an Action object that can be used to set the PassiveToActive outputs to the given pinState value.</summary>
        IActiveIOSetOutputsAction CreateSetOutputsAction(ActiveToPassivePinsState pinsState);
    }

    #endregion
}
