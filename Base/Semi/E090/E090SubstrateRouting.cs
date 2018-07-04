//-------------------------------------------------------------------
/*! @file E090SubstrateRouting.cs
 *  @brief 
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2018 Mosaic Systems Inc.
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

using MosaicLib;
using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Part;
using MosaicLib.Semi.E039;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.Semi.E090.SubstrateRouting
{
    /// <summary>
    /// A substrate routing manager is a part that supports this interface.
    /// </summary>
    /// <remarks>
    /// It is generally a management object that coordinates, and abstracts, the behavior of one or more tool type specific robots and all directly related hardware (such as aligners).
    /// It is used to present a unified and consistent substrate routing interface to higher level entities (usually scheduler(s)).
    /// It is often also used to provide a consistent set of higher level service actions that may be used for recovery and operator assistance related behaviors.
    /// Generally classes of this type are expected to make extesive use of E090 and E039 so as to be able to map the individual SubsrateRoutingItems to actual robot actions.
    /// Generally classes of this type are expected to reflect the results of all requested items (successfully completed or not) back into the E090/E039 object table space.  
    /// It is the responsibility of the client code to recognize and handle any resulting subsrate location state as needed.
    /// This interface is intended to support an arbitrary amount of queueing and/or concurrency.  
    /// The individual implementation objects may run the given request actions in the order provided or may schedule them or interleave their items as desired in an implementation specific manner.
    /// Individual implementation objects are responsible for all error handling and recovery behaviors.
    /// </remarks>
    public interface ISubstrateRoutingManager : IActivePartBase
    {
        INotificationObject<INamedValueSet> DetailsNVSPublisher { get; }

        IClientFacet Sequence(params SubstrateRoutingItemBase [] itemParamsArray);
    }

    [Obsolete("Warning: this class is not currently in use. (2018-06-20)")]
    public class SubstrateRoutingManagerDetails
    {
        public SubstrateRoutingManagerDetails(INamedValueSet detailsNVS)
        {
            var nvs = detailsNVS.MapNullToEmpty();

            KnownSubstLocNameList = nvs["KnownSubstLocNameList"].VC.GetValue<ReadOnlyIList<string>>(rethrow: false).MapNullToEmpty();
            KnownProxySubstLocNameList = nvs["KnownProxySubstLocNameList"].VC.GetValue<ReadOnlyIList<string>>(rethrow: false).MapNullToEmpty();
            SupportsSwapAtSubstLocNameList = nvs["SupportsSwapAtSubstLocNameList"].VC.GetValue<ReadOnlyIList<string>>(rethrow: false).MapNullToEmpty();
            SupportsSwapAtAllLocations = nvs["SupportsSwapAtAllLocations"].VC.GetValue<bool>(rethrow: false);
        }

        public void AddDetailsTo(NamedValueSet nvs)
        {
            nvs.ConditionalSetValue("KnownSubstLocNameList", !KnownSubstLocNameList.IsNullOrEmpty(), KnownSubstLocNameList);
            nvs.ConditionalSetValue("KnownProxySubstLocNameList", !KnownProxySubstLocNameList.IsNullOrEmpty(), KnownProxySubstLocNameList);
            nvs.ConditionalSetValue("SupportsSwapAtSubstLocNameList", !SupportsSwapAtSubstLocNameList.IsNullOrEmpty(), SupportsSwapAtSubstLocNameList);
            nvs.ConditionalSetValue("SupportsSwapAtAllLocations", SupportsSwapAtAllLocations, SupportsSwapAtAllLocations);
        }

        public IList<string> KnownSubstLocNameList { get; set; }
        public IList<string> KnownProxySubstLocNameList { get; set; }

        public IList<string> SupportsSwapAtSubstLocNameList { get; set; }
        public bool SupportsSwapAtAllLocations { get; set; }
    }

    /// <summary>
    /// This is the required base class for all Subsrate Routing Items that can be Run using a <seealso cref="ISubstrateRoutingManager"/>
    /// </summary>
    public class SubstrateRoutingItemBase
    { }

    /// <summary>
    /// This item has two forms.  In the first it is given a Substrate ID, and in the second it is given the location name of a robot or of a robot arm.
    /// When executed, this item will first determine which robot arm shall be used for the approach, either from the current location of the given substrate, or from the given robot or robot arm location name.
    /// Then it will determine if the arm is occupied and then it will either move to the pre-pick or the pre-place position for that arm and occupation status.
    /// If MustSucceed is true any issue or failure was encountered with this item then the sequence it is being run within will fail, otherwise an any such issue will be logged but the seuqence will not fail due to this item.
    /// </summary>
    public class ApproachLocationItem : SubstrateRoutingItemBase
    {
        public ApproachLocationItem(E039ObjectID substID, string toSubstLocName, bool waitUntilDone = false, bool mustSucceed = false)
        {
            SubstID = substID;
            ToSubstLocName = toSubstLocName;
            WaitUntilDone = waitUntilDone;
            MustSucceed = mustSucceed;
        }

        public ApproachLocationItem(string robotOrRobotArmLocName, string toSubstLocName, bool waitUntilDone = false, bool mustSucceed = false)
        {
            RobotOrRobotArmLocName = robotOrRobotArmLocName;
            ToSubstLocName = toSubstLocName;
            WaitUntilDone = waitUntilDone;
            MustSucceed = mustSucceed;
        }

        public E039ObjectID SubstID { get; private set; }
        public string RobotOrRobotArmLocName { get; private set; }
        public string ToSubstLocName { get; private set; }
        public bool WaitUntilDone { get; private set; }
        public bool MustSucceed { get; private set; }

        public override string ToString()
        {
            string waitUntilDoneStr = WaitUntilDone ? "" : " [WaitUntilDone]";
            string mustSucceedStr = MustSucceed ? "" : " [MustSucceed]";

            if (!SubstID.IsNullOrEmpty())
                return "{0} {1} toLocName:{2}{3}{4}".CheckedFormat(Fcns.CurrentClassLeafName, SubstID.FullName, ToSubstLocName, waitUntilDoneStr, mustSucceedStr);
            else
                return "{0} {1} toLocName:{2}{3}{4}".CheckedFormat(Fcns.CurrentClassLeafName, RobotOrRobotArmLocName ?? "[NoLocGiven]", ToSubstLocName, waitUntilDoneStr, mustSucceedStr);
        }
    }

    /// <summary>
    /// When executed, this item will request that the given substrate be moved from its current location to the location with the given name.
    /// This operation generally requires that a routing path can be planned that is currently unoccupied and that all required source and destination locations can be locked or prepared for transfer successfully.
    /// </summary>
    public class MoveSubstrateItem : SubstrateRoutingItemBase
    {
        public MoveSubstrateItem(E039ObjectID substID, string toSubstLocName) 
        {
            SubstID = substID.MapNullToEmpty();
            ToSubstLocName = toSubstLocName;
        }

        public E039ObjectID SubstID { get; private set; }
        public string ToSubstLocName { get; private set; }

        public override string ToString()
        {
            return "{0} {1} toLocName:{2}".CheckedFormat(Fcns.CurrentClassLeafName, SubstID.FullName, ToSubstLocName);
        }
    }

    /// <summary>
    /// When executed, this item will request that the given substrate be moved to a robot arm (if not already there) and then that the robot perform a swap with the given swap with substrate at the swap with substrate's current location
    /// </summary>
    public class SwapSubstratesItem : SubstrateRoutingItemBase
    {
        public SwapSubstratesItem(E039ObjectID substID, E039ObjectID swapWithSubstID)
        {
            SubstID = substID.MapNullToEmpty();
            SwapWithSubstID = swapWithSubstID.MapNullToEmpty();
        }

        public E039ObjectID SubstID { get; private set; }
        public E039ObjectID SwapWithSubstID { get; private set; }

        public override string ToString()
        {
            return "{0} {1} swapWith:{2}".CheckedFormat(Fcns.CurrentClassLeafName, SubstID.FullName, SwapWithSubstID.FullName);
        }
    }

    /// <summary>
    /// When executed, this item will request that the given substrate be moved from its current location to the location with the given name.
    /// If the location already contains a substrate and the robot has an available arm then the substrate at the given location will be moved to the robot first.
    /// This operation generally requires that a routing path can be planned that is currently unoccupied and that all required source and destination locations can be locked or prepared for transfer successfully.
    /// </summary>
    public class MoveOrSwapSubstrateItem : SubstrateRoutingItemBase
    {
        public MoveOrSwapSubstrateItem(E039ObjectID substID, string toSubstLocName)
        {
            SubstID = substID.MapNullToEmpty();
            ToSubstLocName = toSubstLocName;
        }

        public E039ObjectID SubstID { get; private set; }
        public string ToSubstLocName { get; private set; }

        public override string ToString()
        {
            return "{0} {1} toLocName:{2}".CheckedFormat(Fcns.CurrentClassLeafName, SubstID.FullName, ToSubstLocName);
        }
    }

    /// <summary>
    /// When executed, this item will run, or start, the given IClientFacet action.
    /// <para/>ICFFactoryDelegate may be used in place of the ICF to dynamically construct the action to be run in which case this item's ICF property will be internally assigned to the result of calling the ICFFactoryDelegate.
    /// </summary>
    public class RunActionItem : SubstrateRoutingItemBase
    {
        public RunActionItem(IClientFacet icf, RunActionBehaviorFlags runActionBehavior = RunActionBehaviorFlags.Normal)
        {
            ICF = icf;
            Behavior = runActionBehavior;
        }

        public RunActionItem(Func<IClientFacet> icfFactoryDelegate, RunActionBehaviorFlags runActionBehavior = RunActionBehaviorFlags.Normal)
        {
            ICFFactoryDelegate = icfFactoryDelegate;
            Behavior = runActionBehavior;

            createICFMutex = new object();
        }

        public IClientFacet ICF { get; private set; }
        public Func<IClientFacet> ICFFactoryDelegate { get; private set; }
        public RunActionBehaviorFlags Behavior { get; private set; }

        private object createICFMutex = new object();

        public IClientFacet CreateICFIfNeeded()
        {
            if (ICF == null && createICFMutex != null)
            {
                lock (this)
                {
                    if (ICF == null && ICFFactoryDelegate != null)
                        return (ICF = ICFFactoryDelegate());
                }
            }

            return ICF;
        }
    }

    /// <summary>
    /// Flags used with RunActionItem.
    /// <para/>Normal (0x00), OnlyStartAction (0x01), IgnoreFailures (0x02)
    /// </summary>
    [Flags]
    public enum RunActionBehaviorFlags : int
    {
        /// <summary>Default placeholder.  When used alone the item runs the action to completion and requires that it suceeded (0x00)</summary>
        Normal = 0x00,

        /// <summary>This flag requests that the item only require that the action could be successfully started.  It does not wait for the action to complete (0x01)</summary>
        OnlyStartAction = 0x01,

        /// <summary>This flag indicates that the item's creator does not want any sequence that this item is used in to fail, even if the resulting action Run (or Start) failed.</summary>
        IgnoreFailures = 0x02,
    }

    /// <summary>
    /// When executed, this item can be used to acquire or release transfer permission for a set of one or more location names.
    /// The settings are used to control the detail of how this item will be executed.
    /// </summary>
    public class TransferPermissionRequestItem : SubstrateRoutingItemBase
    {
        public TransferPermissionRequestItem(TransferPermissionRequestItemSettings settings, params string [] locNameParamsArray)
        { 
            Settings = settings;
            LocNameList = locNameParamsArray.ConvertToReadOnly();
        }

        public TransferPermissionRequestItemSettings Settings { get; private set; }
        public ReadOnlyIList<string> LocNameList { get; private set; }
    }

    /// <summary>
    /// Settings flags enum for TransferPermissionRequestItems
    /// <para/>None (0x000), StartRequest (0x001), Acquire (0x010), AcquireOnlyIfNeeded (0x020), AutoReleaseAtEndOfSequence (0x040), Release (0x100), ReleaseIfNeeded (0x200)
    /// </summary>
    [Flags]
    public enum TransferPermissionRequestItemSettings : int
    {
        /// <summary>Placeholder default [0x000]</summary>
        None = 0x000,

        /// <summary>Causes the current item to post the create and start the given request, but not to wait for its completion so that the following sequence items that do not need use to the permission can run in parallel with the corresponding request.</summary>
        OnlyStartRequest = 0x001,

        /// <summary>Requests that the item check the last published transfer permission token set, and that it only attempt to acquire the permission for the indicated location/token name (or empty string for parts that support it) if the token set does not already contain the indicated location/token name.</summary>
        Acquire = 0x010,

        /// <summary>Requests that the target part acquire and retain transfer permission for the indicated location/token name (or empty string for parts that support it).  If the permission has already been obtained for this location/token, the location/token is added to the token set anyways.</summary>
        RecursiveAcquire = 0x020,

        /// <summary>Requests that the explicitly acquired transfer permission will be released automatically at the end of the sequence if it has not been manually released by a prior item already</summary>
        AutoReleaseAtEndOfSequence = 0x040,

        /// <summary>Requests that the target part release one occurrence of the indicated location/token name (or empty string for parts that support it) from its transfer permission token set.</summary>
        Release = 0x100,
    }

    /// <summary>
    /// Public interface to be supported by parts that can be used with TransferPermissionRequestItems above.  
    /// This interface allows an SRM to determine what locations the SRM already has transfer permission for and allows it to acquire and release permissions on a per location name basis.</summary>
    public interface ITransferPermissionRequest
    {
        /// <summary>Action factory method.  When run the resulting action will attempt to perform the given <paramref name="requestType"/> on the part for the given <paramref name="locName"/> (which defaults to the empty string)</summary>
        IClientFacet TransferPermission(TransferPermissionRequestType requestType, string locName = "");

        /// <summary>Returns the notification object and publisher used to obtain and track publication of changes to the ITokenSet{string} that represents the set of location names (or empty string for parts that support it) for which the part has granted transfer permission.</summary>
        INotificationObject<ITokenSet<string>> TransferPermissionStatePublisher { get; }
    }

    /// <summary>
    /// Used with the TransferPermission action factory method to specify the type of permission request to perform.
    /// <para/>None (0), Acquire, Release, ReleaseAll
    /// </summary>
    public enum TransferPermissionRequestType : int
    {
        /// <summary>Default placeholder value [0]</summary>
        None = 0,

        /// <summary>
        /// Requests that the target transition to a state where transfer can take place (possibly for the given locName) and that it lock itself in this state until the permission has been released.
        /// <para/>Note that if this request is applied more than once for the same locName (or key), multiple instances of the given locName (key) will be added to the permission granted list.
        /// </summary>
        Acquire,

        /// <summary>Attempts to remove the given locName from the permission granted list.  Once a given locName is no longer in the list then the target part may unlock motion for the corresponding location.</summary>
        Release,

        /// <summary>Removes all granted permissions for all locations.  This is typically only done after homing the robot(s) and verifying that there are no additional outstanding transfers in progress in relation to this location.</summary>
        ReleaseAll,
    }

    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given <paramref name="transferPermissionTokenSet"/> is non-null and it contains the given key (token)
        /// </summary>
        public static bool GetIsTransferPermitted(this ITokenSet<string> transferPermissionTokenSet, string key)
        {
            return (transferPermissionTokenSet.SafeContains(key));
        }

        public static SubstrateRoutingItemBase AttemptToGenerateSubstrateRoutingItem(this IStringParamAction serviceAction)
        {
            string serviceActionParam = serviceAction.ParamValue;
            INamedValueSet actionNVP = serviceAction.NamedParamValues;

            return AttemptToGenerateSubstrateRoutingItem(serviceActionParam, actionNVP);
        }

        private static SubstrateRoutingItemBase AttemptToGenerateSubstrateRoutingItem(string serviceActionParam, INamedValueSet actionNVP)
        {
            switch (serviceActionParam)
            {
                case "Move": return new MoveSubstrateItem(actionNVP["SubstName"].VC.GetValue<string>(rethrow: false).CreateE090SubstID(), actionNVP["ToSubstLocName"].VC.GetValue<string>(rethrow: false));
                case "Swap": return new SwapSubstratesItem(actionNVP["SubstName"].VC.GetValue<string>(rethrow: false).CreateE090SubstID(), actionNVP["SwapWithSubstName"].VC.GetValue<string>(rethrow: false).CreateE090SubstID());
                default: return null;
            }
        }
    }
}
