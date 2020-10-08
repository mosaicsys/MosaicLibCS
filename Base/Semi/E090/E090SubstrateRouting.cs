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
using MosaicLib.Time;

namespace MosaicLib.Semi.E090.SubstrateRouting
{
    /*
     * The classes and defintions here are generally used as part of the Scheduler related addition to the E090 namespace concepts.
     * 
     * SubstrateRouting covers the concept of compositional parts and objects that can be used to assist in, and to help simplify and normalize,
     * The work required to move substrates, typically using one or more robot instances (devices that support moving material from place to place).
     * 
     * The basic concepts used here are embodied in the ISubstrateRoutingManager interface, the various types of SubstrateRoutingItemBase derived types such as
     * MoveSubstrateItem, SwapSubstratesItem, MoveOrSwapSubstrateItem and ApproachLocationItem, the the various ITransferPermission related interfaces and types.
     * 
     * The SRM (ISubstrateRoutingManager) concept starts by given the client scheudling and service action code an E090 centric use model for directing typical material movement
     * activities.  It also acts as a common point of implementation for robot related interlocks as embodied in the ITransferPermissionRequest interface by supporting a
     * location to ITPR instance map that is used automatically to verify and interlock access to a given location while the robot is performing the physical material movement
     * operations that generally needs to be interlocked to avoid interfearance with other hardware at that location.
     * 
     * In addition it is envisioned that the SRM will also be used to embody the first level of common E041 Annunciator usage to support giving the qualified user/decision tree 
     * authority the ability to give informed and considered path selection for specific robot and location fault conditions that are not well described and managed at the scheduler level.
     * 
     * The SRMBase class can be used to provide some/most of the common code implementation of a given SRM instance.  It is used as the base for the TestSRM found under the 
     * MosaicLib.Semi.E090.SubstrateTestingTools namespace.
     */

    #region ISubstrateRoutingManager

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
        /// <summary>Action factory method:  When run, the resulting action will attempt to retract all robot arms and will then release all held ITPR interfaces that this instance knows about.</summary>
        IClientFacet RetractArmsAndReleaseAll(bool optimizeReleasing = true);

        /// <summary>Action factory method:  When run, the resulting action will attempt to execute each of the items in the given sequence of items.</summary>
        IClientFacet Sequence(params SubstrateRoutingItemBase[] itemParamsArray);
    }

    #endregion

    #region SubstrateRoutingItemBase

    /// <summary>
    /// This is the required base class for all Subsrate Routing Items that can be Run using a <seealso cref="ISubstrateRoutingManager"/>
    /// </summary>
    public class SubstrateRoutingItemBase
    {
        /// <summary>
        /// This property is included for client purposes.  It is generally used to help identify the source of this routing item for logging purposes.
        /// </summary>
        public string Comment { get; set; }
    }

    #endregion

    #region ApproachLocationItem, MoveSubstrateItem, SwapSubstratesItem, MoveOrSwapSubstrateItem, RunActionItem

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
            string commentStr = Comment.IsNeitherNullNorEmpty() ? " Comment:'{0}'".CheckedFormat(Comment) : "";

            if (!SubstID.IsNullOrEmpty())
                return "{0} {1} toLocName:{2}{3}{4}{5}".CheckedFormat(Fcns.CurrentClassLeafName, SubstID.FullName, ToSubstLocName, waitUntilDoneStr, mustSucceedStr, commentStr);
            else
                return "{0} {1} toLocName:{2}{3}{4}{5}".CheckedFormat(Fcns.CurrentClassLeafName, RobotOrRobotArmLocName ?? "[NoLocGiven]", ToSubstLocName, waitUntilDoneStr, mustSucceedStr, commentStr);
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
            string commentStr = Comment.IsNeitherNullNorEmpty() ? " Comment:'{0}'".CheckedFormat(Comment) : "";

            return "{0} {1} toLocName:{2}{3}".CheckedFormat(Fcns.CurrentClassLeafName, SubstID.FullName, ToSubstLocName, commentStr);
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
            string commentStr = Comment.IsNeitherNullNorEmpty() ? " Comment:'{0}'".CheckedFormat(Comment) : "";

            return "{0} {1} swapWith:{2}{3}".CheckedFormat(Fcns.CurrentClassLeafName, SubstID.FullName, SwapWithSubstID.FullName, commentStr);
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
            string commentStr = Comment.IsNeitherNullNorEmpty() ? " Comment:'{0}'".CheckedFormat(Comment) : "";

            return "{0} {1} toLocName:{2}{3}".CheckedFormat(Fcns.CurrentClassLeafName, SubstID.FullName, ToSubstLocName, commentStr);
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
                lock (createICFMutex)
                {
                    if (ICF == null && ICFFactoryDelegate != null)
                        return (ICF = ICFFactoryDelegate());
                }
            }

            return ICF;
        }

        public override string ToString()
        {
            string commentStr = Comment.IsNeitherNullNorEmpty() ? " Comment:'{0}'".CheckedFormat(Comment) : "";

            if (ICF != null)
                return "{0} {1} {2}{3}".CheckedFormat(Fcns.CurrentClassLeafName, Behavior, ICF, commentStr);
            else if (ICFFactoryDelegate != null)
                return "{0} {1} waiting to create ICF{2}".CheckedFormat(Fcns.CurrentClassLeafName, Behavior, commentStr);
            else
                return "{0} {1} [NoICF,NoFactory]{2}".CheckedFormat(Fcns.CurrentClassLeafName, Behavior, commentStr);
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

    #endregion

    #region ITransferPermissionRequest, TransferPermissionRequestType, EMs

    /// <summary>
    /// Public interface to be supported by parts that can be used with TransferPermissionRequestItems above.  
    /// This interface allows an SRM to determine what locations the SRM already has transfer permission for and allows it to acquire and release permissions on a per location name basis.
    /// </summary>
    public interface ITransferPermissionRequest
    {
        /// <summary>Action factory method.  When run the resulting action will attempt to perform the given <paramref name="requestType"/> on the part for the given <paramref name="locName"/> (which defaults to the empty string)</summary>
        IClientFacet TransferPermission(TransferPermissionRequestType requestType, string locName = "");

        /// <summary>Returns the notification object and publisher used to obtain and track publication of changes to the ITransferPermissionState that represents a part's transfer permission state (it may have more than one such state at a time if it has multiple ports of access).</summary>
        INotificationObject<ITransferPermissionState> TransferPermissionStatePublisher { get; }
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

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        [Obsolete("Use of this method is no longer supported.  Change to the use of the related ITransferPermissionState EMs.  (2018-12-15)")]
        public static bool GetIsTransferPermitted(this ITokenSet<string> transferPermissionTokenSet, string key)
        {
            throw new System.NotImplementedException();
        }
    }

    #endregion

    #region ITransferPermissionState, TransferPermissionSummaryStateCode, TransferPermissionStateBase, EMs

    /// <summary>
    /// This interface defines the set of inforamation that makes up the "TransferPermissionState" of some part that supports the ITransferPermissionRequest interface.
    /// <para/>Primarily this includes a SummaryStateCode and a GrantedTokenSet.
    /// </summary>
    public interface ITransferPermissionState
    {
        /// <summary>
        /// Gives the name of the specific ITransferPermissionRequest interface that this state relates to.  May be empty if the source only has one such interface.
        /// </summary>
        string InterfaceName { get; }

        /// <summary>
        /// Gives the summary of the current transfer permission state. 
        /// </summary>
        TransferPermissionSummaryStateCode SummaryStateCode { get; }

        /// <summary>
        /// Gives the time stamp of the last change in the SummaryStateCode
        /// </summary>
        QpcTimeStamp SummaryStateTimeStamp { get; }

        /// <summary>
        /// Gives a description of the current summary state code, if relevant.  
        /// <para/>This is normally used for the NotAvailable state to describe why the state of this part is not available to grant transfer permission.
        /// However it may also be used for other states
        /// </summary>
        string Reason { get; }

        /// <summary>
        /// Gives the estimated time period after which this part expects to be available to grant transfer permission requests.  
        /// It is expected that it will be given a non-zero value when in the AlmostAvailable state, and zero otherwise.
        /// </summary>
        TimeSpan EstimatedAvailableAfterPeriod { get; }

        /// <summary>
        /// This token set gives the set of location names for which the part has granted permission for a transfer.  
        /// This interface expects (requires) that the part only grant such permission when it can internally attempt to enforce that no other action it takes will be likely to interfear with any related externally triggered transfer activities.
        /// It is up to the part granting transfer permission to internally enforce these conditions.
        /// </summary>
        ITokenSet<string> GrantedTokenSet { get; }
    }

    /// <summary>
    /// This is enumeration represents a summary of the transfer permissions state for a part that supports this interface.
    /// <para/>None (0), Available, Busy, AlmostAvailable, NotAvailable
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum TransferPermissionSummaryStateCode : int
    {
        /// <summary>
        /// Placeholder default value. [0]
        /// </summary>
        [EnumMember]
        None = 0,

        /// <summary>
        /// The part currently believes that the corresponding ITPR interface is available to grant transfer permission requests.  
        /// Transfer permission may (or may not) have been granted in this state.
        /// </summary>
        [EnumMember]
        Available,

        /// <summary>
        /// The part is indicating that it is busy performing some action (such a processing step) and that it will not be available until it has finished being busy.
        /// <para/>Generally this state is followed by Available or AlmostAvailable depending on the part internal details.
        /// <para/>The expected behavior of ITPR Acquire requests in this state is that they will complete successfully after the part is no longer Busy.  
        /// However the client is generally expected to wait until the part is Available or almost available before making ITPR Acquire requests.
        /// </summary>
        [EnumMember]
        Busy,

        /// <summary>
        /// The part believes that it has almost finished some activity and that it will be available after it has finished that activity.  
        /// Generally the part provides a non-zero EstimatedAvailableAfterPeriod value while in this state.  
        /// This value can be used with the SummaryStateTimeStamp to estimate the time at which the interface is next expected to become available.
        /// <para/>The expected behavior of ITPR Acquire requests in this state is that they will complete successfully after the part is reached the Available state.  However the client is generally expected to wait until the part is actually Available before doing this.
        /// </summary>
        [EnumMember]
        AlmostAvailable,

        /// <summary>
        /// Some unexpected condition is active that means that the interface is not availalbe and that it will not automatically become available after some period of time without external intervention.
        /// <para/>Generally the part will indicate the current reason why it is in this state using the Reason property.
        /// <para/>The expected behavior of ITPR Acquire requests in this state is that they will be immediately rejected.
        /// </summary>
        [EnumMember]
        NotAvailable,

        /// <summary>
        /// This state is typically used when the part has granted ITPR access on another physical port (another ITPR interface) and this ITPR interface will not be avilable until the other one has released its access.
        /// <para/>The expected behavior of ITPR Acquire requests in this state is that they will complete successfully after the part is no longer Blocked.  However the client is generally expected to wait until the part is Available before doing this.
        /// </summary>
        [EnumMember]
        Blocked,
    }

    /// <summary>
    /// This class is usable as the implementation class for supporting ITransferPermissionState publication, and/or (possibly) as the base class for derived types that extend this capability.
    /// </summary>
    public class TransferPermissionState : ITransferPermissionState, IEquatable<ITransferPermissionState>, ICopyable<ITransferPermissionState>
    {
        /// <summary>
        /// Default constructor.  optionally constructs a new r/w instance with an empty, writeable GrantedTokenSet.
        /// </summary>
        public TransferPermissionState(bool createRWTokenSet = true) 
        {
            if (createRWTokenSet)
                _grantedTokenSet = new TokenSet<string>();
        }

        /// <summary>
        /// Gives the name of the specific ITransferPermissionRequest interface that this state relates to.  May be empty if the source only has one such interface.
        /// </summary>
        public string InterfaceName { get { return _interfaceName.MapNullToEmpty(); } set { _interfaceName = value; } }
        private string _interfaceName;

        /// <summary>
        /// Gives the summary of the current transfer permission state. 
        /// </summary>
        public TransferPermissionSummaryStateCode SummaryStateCode { get; set; }

        /// <summary>
        /// Gives the time stamp of the last change in the SummaryStateCode
        /// </summary>
        public QpcTimeStamp SummaryStateTimeStamp { get; set; }

        /// <summary>
        /// Gives a description of the current summary state code, if relevant.  
        /// <para/>This is normally used for the NotAvailable state to describe why the state of this part is not available to grant transfer permission.
        /// However it may also be used for other states.
        /// </summary>
        public string Reason { get { return _reason.MapNullToEmpty(); } set { _reason = value; } }
        private string _reason;

        /// <summary>
        /// Gives the estimated time period after which this part expects to be available to grant transfer permission requests.  
        /// It is expected that it will be given a non-zero value when in the AlmostAvailable state.
        /// </summary>
        public TimeSpan EstimatedAvailableAfterPeriod  { get; set; }

        /// <summary>
        /// This token set gives the set of location names for which the part has granted permission for a transfer.  
        /// This interface expects (requires) that the part only grant such permission when it can internally attempt to enforce that no other action it takes will be likely to interfear with any related externally triggered transfer activities.
        /// It is up to the part granting transfer permission to internally enforce these conditions.
        /// </summary>
        public ITokenSet<string> GrantedTokenSet 
        { 
            get { return _grantedTokenSet.MapNullToEmpty(); }
            set { _grantedTokenSet = value; } 
        }

        private ITokenSet<string> _grantedTokenSet;

        /// <summary>
        /// Returns true if this object has the same contents as the given <paramref name="other"/> object.
        /// </summary>
        public virtual bool Equals(ITransferPermissionState other, bool compareTimeStamps = true)
        {
            return (other != null
                    && InterfaceName == other.InterfaceName
                    && SummaryStateCode == other.SummaryStateCode
                    && (!compareTimeStamps || (SummaryStateTimeStamp == other.SummaryStateTimeStamp))
                    && Reason == other.Reason
                    && EstimatedAvailableAfterPeriod == other.EstimatedAvailableAfterPeriod
                    && GrantedTokenSet.Equals(other.GrantedTokenSet, compareReadOnly: false)
                    );
        }

        bool IEquatable<ITransferPermissionState>.Equals(ITransferPermissionState other)
        {
            return this.Equals(other, compareTimeStamps: true);
        }

        public virtual ITransferPermissionState MakeCopyOfThis(bool deepCopy = true)
        {
            return new TransferPermissionState(createRWTokenSet: false)
            {
                InterfaceName = InterfaceName.MapNullToEmpty(),
                SummaryStateCode = SummaryStateCode,
                SummaryStateTimeStamp = SummaryStateTimeStamp,
                Reason = Reason.MapNullToEmpty(),
                EstimatedAvailableAfterPeriod = EstimatedAvailableAfterPeriod,

                GrantedTokenSet = GrantedTokenSet.ConvertToReadOnly(mapNullToEmpty: true),
            };
        }

        /// <summary>
        /// Debugging and logging helper method
        /// </summary>
        public override string ToString()
        {
            string interfaceName = InterfaceName;
            string interfaceNameStr = (interfaceName.IsNullOrEmpty() ? "" : " " + interfaceName);

            if (EstimatedAvailableAfterPeriod.IsZero())
                return "TPS{0} State:{1} Reason:'{2}' {3}".CheckedFormat(interfaceNameStr, SummaryStateCode, Reason, GrantedTokenSet);
            else
                return "TPS{0} State:{1} Reason:'{2}' {3} EstAvailDelay:{4:f3} sec".CheckedFormat(interfaceNameStr, SummaryStateCode, Reason, GrantedTokenSet, EstimatedAvailableAfterPeriod.TotalSeconds);
        }
    }

    /// <summary>
    /// This class is intended to be used to support publication of an ITransferPermissionState derived state via an IVAs or via other mechanisms that require serialization support.
    /// Instances of this class are immutable.
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public class TransferPermissionStateForPublication : ITransferPermissionState, IEquatable<ITransferPermissionState>
    {
        public static ITransferPermissionState Empty { get { return _empty; } }
        private static readonly ITransferPermissionState _empty = new TransferPermissionStateForPublication();

        /// <summary>
        /// Default constructor.  Constructs a new r/w instance with an empty, writeable GrantedTokenSet.
        /// </summary>
        public TransferPermissionStateForPublication()
        {
            _grantedTokenSet = new TokenSet<string>();
        }

        /// <summary>
        /// Copy constructor.  By default, the resulting object is initialized to be readonly.
        /// </summary>
        public TransferPermissionStateForPublication(ITransferPermissionState other)
        {
            _interfaceName = other.InterfaceName.MapEmptyToNull();
            SummaryStateCode = other.SummaryStateCode;
            SummaryStateTimeStamp = other.SummaryStateTimeStamp;
            _reason = other.Reason.MapEmptyToNull();
            EstimatedAvailableAfterPeriod = other.EstimatedAvailableAfterPeriod;

            GrantedTokenSet = other.GrantedTokenSet.ConvertToWritable(mapNullToEmpty: true);
        }

        /// <summary>
        /// Gives the name of the specific ITransferPermissionRequest interface that this state relates to.  May be empty if the source only has one such interface.
        /// </summary>
        public string InterfaceName { get { return _interfaceName.MapNullToEmpty(); } }

        [DataMember(Order = 1000, Name = "InterfaceName", IsRequired = false, EmitDefaultValue = false)]
        private string _interfaceName;

        /// <summary>
        /// Gives the summary of the current transfer permission state. 
        /// </summary>
        [DataMember(Order = 2000, IsRequired = false, EmitDefaultValue = false)]
        public TransferPermissionSummaryStateCode SummaryStateCode { get; private set; }

        /// <summary>
        /// Gives the time stamp of the last change in the SummaryStateCode
        /// </summary>
        public QpcTimeStamp SummaryStateTimeStamp { get; private set; }

        [DataMember(Order = 3000, IsRequired = false, EmitDefaultValue = false)]
        private double? SummaryStateTimeStampAgeInSec
        {
            get { return !SummaryStateTimeStamp.IsZero ? SummaryStateTimeStamp.Age.TotalSeconds : (double?)null; }
            set { SummaryStateTimeStamp = (value != null) ? (QpcTimeStamp.Now - (value ?? 0.0).FromSeconds()) : QpcTimeStamp.Zero; }
        }

        /// <summary>
        /// Gives a description of the current summary state code, if relevant.  
        /// <para/>This is normally used for the NotAvailable state to describe why the state of this part is not available to grant transfer permission.
        /// However it may also be used for other states.
        /// </summary>
        public string Reason { get { return _reason.MapNullToEmpty(); } }

        [DataMember(Order = 4000, Name = "Reason", IsRequired = false, EmitDefaultValue = false)]
        private string _reason;

        /// <summary>
        /// Gives the estimated time period after which this part expects to be available to grant transfer permission requests.  
        /// It is expected that it will be given a non-zero value when in the AlmostAvailable state.
        /// </summary>
        [DataMember(Order = 5000, IsRequired = false, EmitDefaultValue = false)]
        public TimeSpan EstimatedAvailableAfterPeriod { get; private set; }

        /// <summary>
        /// This token set gives the set of location names for which the part has granted permission for a transfer.  
        /// This interface expects (requires) that the part only grant such permission when it can internally attempt to enforce that no other action it takes will be likely to interfear with any related externally triggered transfer activities.
        /// It is up to the part granting transfer permission to internally enforce these conditions.
        /// </summary>
        public ITokenSet<string> GrantedTokenSet { get { return _grantedTokenSet.MapNullToEmpty(); } private set { _grantedTokenSet = value; } }

        private ITokenSet<string> _grantedTokenSet;

        [DataMember(Order = 6000, Name = "GrantedTokenSet", IsRequired = false, EmitDefaultValue = false)]
        private string[] GrantedTokenSetDCSProxyArray { get { return GrantedTokenSet.ToArray().MapEmptyToNull(); } set { GrantedTokenSet = new TokenSet<string>(value.MapNullToEmpty()).MapEmptyToNull().ConvertToReadOnly(mapNullToEmpty: false); } }

        /// <summary>
        /// Returns true if this object has the same contents as the given <paramref name="other"/> object.
        /// </summary>
        public virtual bool Equals(ITransferPermissionState other, bool compareTimeStamps = true)
        {
            return (other != null
                    && InterfaceName == other.InterfaceName
                    && SummaryStateCode == other.SummaryStateCode
                    && (!compareTimeStamps || (SummaryStateTimeStamp == other.SummaryStateTimeStamp))
                    && Reason == other.Reason
                    && EstimatedAvailableAfterPeriod == other.EstimatedAvailableAfterPeriod
                    && GrantedTokenSet.Equals(other.GrantedTokenSet, compareReadOnly: false)
                    );
        }

        bool IEquatable<ITransferPermissionState>.Equals(ITransferPermissionState other)
        {
            return this.Equals(other, compareTimeStamps: true);
        }

        /// <summary>
        /// Debugging and logging helper method
        /// </summary>
        public override string ToString()
        {
            string interfaceName = InterfaceName;
            string interfaceNameStr = (interfaceName.IsNullOrEmpty() ? "" : " " + interfaceName);

            if (EstimatedAvailableAfterPeriod.IsZero())
                return "TPS{0} State:{1} Reason:'{2}' {3}".CheckedFormat(interfaceNameStr, SummaryStateCode, Reason, GrantedTokenSet);
            else
                return "TPS{0} State:{1} Reason:'{2}' {3} EstAvailDelay:{4:f3} sec".CheckedFormat(interfaceNameStr, SummaryStateCode, Reason, GrantedTokenSet, EstimatedAvailableAfterPeriod.TotalSeconds);
        }
    }
    /// <summary>
    /// Extension methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given <paramref name="itps"/> is non-null and its SummaryStateCode IsAvailable (aka it is Available)
        /// </summary>
        public static bool IsAvailable(this ITransferPermissionState itps)
        {
            return itps != null && itps.SummaryStateCode.IsAvailable();
        }

        /// <summary>
        /// Returns true if the given <paramref name="stateCode"/> is Available
        /// </summary>
        public static bool IsAvailable(this TransferPermissionSummaryStateCode stateCode)
        {
            return stateCode == TransferPermissionSummaryStateCode.Available;
        }

        /// <summary>
        /// Returns true if the given <paramref name="itps"/> is Available or it is AlmostAvailable and the estimated remaining time to become available is less than or equal to the given maxEstimatedAvailableAfterPeriod
        /// </summary>
        public static bool IsAvailableOrAlmostAvailable(this ITransferPermissionState itps, TimeSpan ? maxEstimatedAvailableAfterPeriodIn = null, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            if (itps == null)
                return false;

            if (itps.IsAvailable())
                return true;

            if (itps.SummaryStateCode != TransferPermissionSummaryStateCode.AlmostAvailable || itps.EstimatedAvailableAfterPeriod.IsZero())
                return false;

            if (maxEstimatedAvailableAfterPeriodIn == null)
                return true;

            var maxEstimatedAvailableAfterPeriod = maxEstimatedAvailableAfterPeriodIn ?? TimeSpan.Zero;

            qpcTimeStamp = qpcTimeStamp.MapDefaultToNow();
            var elapsedTimeSinceStatePublished = (qpcTimeStamp - itps.SummaryStateTimeStamp);
            var estimatedAvailableAfterPeriod = itps.EstimatedAvailableAfterPeriod - elapsedTimeSinceStatePublished;
            if (estimatedAvailableAfterPeriod <= maxEstimatedAvailableAfterPeriod)
                return true;

            return false;
        }

        /// <summary>
        /// Returns the estimated time remaining before the source module predicts that it will be available for transfer
        /// </summary>
        public static TimeSpan GetEstimatedTimeToAvailable(this ITransferPermissionState itps, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            TimeSpan result = TimeSpan.Zero;

            if (itps != null)
            {
                var summaryStateAge = (qpcTimeStamp.MapDefaultToNow() - itps.SummaryStateTimeStamp);
                result = TimeSpan.Zero.Max(itps.EstimatedAvailableAfterPeriod - summaryStateAge);
            }

            return result;
        }

        /// <summary>
        /// Returns true if the given <paramref name="itps"/> contains a non-empty GrantedTokenSet and the caller either passed <paramref name="checkAvailable"/> as false or the current SummaryStateCode IsAvailable().
        /// </summary>
        public static bool IsAnyGranted(this ITransferPermissionState itps, bool checkAvailable = true)
        {
            return itps != null && itps.GrantedTokenSet.MapNullToEmpty().IsNotEmpty && (itps.SummaryStateCode.IsAvailable() || !checkAvailable);
        }

        /// <summary>
        /// Returns true if the given <paramref name="itps"/>'s GrantedTokenSet contains the given <paramref name="locName"/> the caller either passed <paramref name="checkAvailable"/> as false or the current SummaryStateCode IsAvailable().
        /// </summary>
        public static bool IsGranted(this ITransferPermissionState itps, string locName, bool checkAvailable = true)
        {
            return itps != null && itps.GrantedTokenSet.SafeContains(locName) && (itps.SummaryStateCode.IsAvailable() || !checkAvailable);
        }

        /// <summary>
        /// This is a temporary proxy for the IsGranted EM defined above.  Please switch to the use of the IsGranted EM.
        /// </summary>
        [Obsolete("This is a temporary proxy for the IsGranted EM defined above.  Please switch to the use of the IsGranted EM (2018-12-15)")]
        public static bool GetIsTransferPermitted(this ITransferPermissionState itps, string key)
        {
            return itps.IsGranted(key, checkAvailable: true);
        }

        /// <summary>
        /// Helper EM used to set the SummaryStateCode in a given <paramref name="tpsb"/> TransferPermissionStateBase object.
        /// </summary>
        public static void SetState(this TransferPermissionState tpsb, TransferPermissionSummaryStateCode stateCode, string reason, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp), TimeSpan estimatedAvailableAfterPeriod = default(TimeSpan), Logging.IMesgEmitter emitter = null, string linePrefix = "TPS")
        {
            var entryStateCode = tpsb.SummaryStateCode;

            tpsb.SummaryStateCode = stateCode;
            tpsb.SummaryStateTimeStamp = qpcTimeStamp.MapDefaultToNow();
            tpsb.Reason = reason.MapNullTo("[GivenReasonWasNull]");
            tpsb.EstimatedAvailableAfterPeriod = estimatedAvailableAfterPeriod;

            if (emitter != null && emitter.IsEnabled)
            {
                string interfaceName = tpsb.InterfaceName;
                string interfaceNameStr = (interfaceName.IsNullOrEmpty() ? linePrefix.MapNullToEmpty() : "{0} {1}".CheckedFormat(linePrefix, interfaceName));

                if (tpsb.EstimatedAvailableAfterPeriod.IsZero())
                    emitter.Emit("{0} state set to {1} [from:{2}, reason:{3}]", interfaceNameStr, tpsb.SummaryStateCode, entryStateCode, tpsb.Reason);
                else
                    emitter.Emit("{0} state set to {1} [from:{2}, estAvailAfter:{3:f3} sec, reason:{4}]", interfaceNameStr, tpsb.SummaryStateCode, entryStateCode, tpsb.EstimatedAvailableAfterPeriod.TotalSeconds, tpsb.Reason);
            }
        }
    }

    #endregion

    #region TransferPermissionRequestItem, TransferPermissionRequestItemSettings

    /// <summary>
    /// When executed, this item can be used to acquire or release transfer permission for a set of one or more location names.
    /// The settings are used to control the detail of how this item will be executed.
    /// </summary>
    public class TransferPermissionRequestItem : SubstrateRoutingItemBase
    {
        public TransferPermissionRequestItem(TransferPermissionRequestItemSettings settings, params string[] locNameParamsArray)
        {
            Settings = settings;
            LocNameList = locNameParamsArray.ConvertToReadOnly();
        }

        public TransferPermissionRequestItemSettings Settings { get; private set; }
        public ReadOnlyIList<string> LocNameList { get; private set; }

        public override string ToString()
        {
            string commentStr = Comment.IsNeitherNullNorEmpty() ? " Comment:'{0}'".CheckedFormat(Comment) : "";

            return "{0} {1} LocName(s):{2}{3}".CheckedFormat(Fcns.CurrentClassLeafName, Settings, String.Join(",", LocNameList), commentStr);
        }
    }

    /// <summary>
    /// Settings flags enum for TransferPermissionRequestItems
    /// <para/>None (0x000), StartRequest (0x001), Acquire (0x010), RecursiveAcquire (0x020), AutoReleaseAtEndOfSequence (0x040), Release (0x100), 
    /// <para/>AcquireAndAutoReleaseAtEndOfSequence (0x060)
    /// </summary>
    [Flags]
    public enum TransferPermissionRequestItemSettings : int
    {
        /// <summary>Placeholder default [0x000]</summary>
        None = 0x000,

        /// <summary>Causes the current item to post the create and start the given request, but not to wait for its completion so that the following sequence items that do not need use to the permission can run in parallel with the corresponding request. [0x001]</summary>
        OnlyStartRequest = 0x001,

        /// <summary>Requests that the item check the last published transfer permission token set, and that it only attempt to acquire the permission for the indicated location/token name (or empty string for parts that support it) if the token set does not already contain the indicated location/token name. [0x010]</summary>
        Acquire = 0x010,

        /// <summary>Requests that the target part acquire and retain transfer permission for the indicated location/token name (or empty string for parts that support it).  If the permission has already been obtained for this location/token, the location/token is added to the token set anyways. [0x020]</summary>
        RecursiveAcquire = 0x020,

        /// <summary>Requests that the explicitly acquired transfer permission will be released automatically at the end of the sequence if it has not been manually released by a prior item already [0x040]</summary>
        AutoReleaseAtEndOfSequence = 0x040,

        /// <summary>Requests that the target part release one occurrence of the indicated location/token name (or empty string for parts that support it) from its transfer permission token set. [0x100]</summary>
        Release = 0x100,

        /// <summary>
        /// (Acquire | AutoReleaseAtEndOfSequence)
        /// <para/>Requests that the item check the last published transfer permission token set, and that it only attempt to acquire the permission for the indicated location/token name (or empty string for parts that support it) if the token set does not already contain the indicated location/token name.
        /// <para/>Also requests that the explicitly acquired transfer permission will be released automatically at the end of the sequence if it has not been manually released by a prior item already.
        /// <para/>[0x060]
        /// </summary>
        AcquireAndAutoReleaseAtEndOfSequence = (Acquire | AutoReleaseAtEndOfSequence),
    }

    #endregion

    #region Query Items (DelegatePredicate, PredicateItemBehavior)

    /// <summary>
    /// This item is used to perform a delegate based predicate item where the caller provides an aribrarty Func{string} which returns a non-empty string when the predicate fails which describes the reason the predicate failed
    /// <para/>WARNING: the user of this item is REQUIRED to understand the limitations and ramifications of using a client provided delegate which will be called by the SRM part thread.  
    /// </summary>
    public class DelegatePredicateItem : SubstrateRoutingItemBase
    {
        /// <summary>
        /// Constructor.
        /// <para/>WARNING: the user of this item is REQUIRED to understand the limitations and ramifications of using a client provided delegate which will be called by the SRM part thread.  
        /// </summary>
        public DelegatePredicateItem(Func<string> predicateDelegate, PredicateItemBehavior itemBehavior, INamedValueSet namedValuesToMergeOnNegativeResult = null)
        {
            PredicateDelegate = predicateDelegate;
        }

        /// <summary>Gives the construction time given predicate delegate that this item is to use.  Returns non-empty string on predicate failure which describes why the predicate failed.</summary>
        public Func<string> PredicateDelegate { get; private set; }

        /// <summary>Gives the value of the enum that is used to select the behavior of the predicate item.  Aka what to  do when it fails.</summary>
        public PredicateItemBehavior Behavior { get; private set; }

        /// <summary>This set of named values will be merged (using AddAndUpdate) into the action's NamedValues if the predicate fails.</summary>
        public INamedValueSet NamedValuesToMergeOnNegativeResult { get; private set; }

        public override string ToString()
        {
            string commentStr = Comment.IsNeitherNullNorEmpty() ? " Comment:'{0}'".CheckedFormat(Comment) : "";
            string nvsStr = (NamedValuesToMergeOnNegativeResult.IsNullOrEmpty() ? "" : " MergeNVSOnFalse:{0}".CheckedFormat(NamedValuesToMergeOnNegativeResult.ToStringSML()));

            return "{0} {2}{3}{4}".CheckedFormat(Fcns.CurrentClassLeafName, Behavior, commentStr, nvsStr);
        }
    }

    /// <summary>
    /// This enumeration defines the resulting behavior of evaluating the predicate within a given predicate item (such as SubstratePredicateItem)
    /// <para/>None (0), NegativeFailsSequence, NegativeEndsSequence
    /// </summary>
    public enum PredicateItemBehavior : int
    {
        /// <summary>Placeholder default [0]</summary>
        None = 0,

        /// <summary>If the selected predicate fails (produces false) then the item causes the sequence to fail.</summary>
        NegativeFailsSequence,

        /// <summary>If the selected predicate fails (produces false) then the item causes the sequence to end (without attempting to explicitly run any additional items)</summary>
        NegativeEndsSequence,
    }

    #endregion

    #region Service action related EMs

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        [Obsolete("Please replace use of this method with direct use of the related AttemptToGenerateSubstrateRoutingItem EM. (2018-12-15)")]
        public static SubstrateRoutingItemBase AttemptToGenerateSubstrateRoutingItem(this IStringParamAction serviceAction, IE039TableObserver tableObserver = null)
        {
            string serviceActionParam = serviceAction.ParamValue;
            INamedValueSet actionNVP = serviceAction.NamedParamValues;

            return tableObserver.AttemptToGenerateSubstrateRoutingItem(serviceActionParam, actionNVP);
        }

        /// <summary>
        /// Attempts to generate a SubstrateRoutingItemBase for the given <paramref name="routingRequest"/> and <paramref name="nvp"/>.
        /// Currently supported <paramref name="routingRequest"/> values include "Approach", "Move" and "Swap".
        /// <para/>"Approach" requires that the given <paramref name="nvp"/> include two strings: robotOrRobotArmLocName and toSubstLocName.
        /// <para/>"Move" requires that the given <paramref name="nvp"/> include two strings: substName and toSubstLocName.
        /// <para/>"Swap" requires that the given <paramref name="nvp"/> include two strings: substName and swapWithSubstName.
        /// <para/>This method 
        /// </summary>
        public static SubstrateRoutingItemBase AttemptToGenerateSubstrateRoutingItem(this IE039TableObserver tableObserver, string routingRequest, INamedValueSet nvp)
        {
            switch (routingRequest)
            {
                case "Approach": return new ApproachLocationItem(nvp["robotOrRobotArmLocName"].VC.GetValueA(rethrow: false), nvp["toSubstLocName"].VC.GetValueA(rethrow: false));
                case "Move": return new MoveSubstrateItem(nvp["substName"].VC.GetValueA(rethrow: false).CreateE090SubstID(tableObserver: tableObserver), nvp["toSubstLocName"].VC.GetValueA(rethrow: false));
                case "Swap": return new SwapSubstratesItem(nvp["substName"].VC.GetValueA(rethrow: false).CreateE090SubstID(tableObserver: tableObserver), nvp["swapWithSubstName"].VC.GetValueA(rethrow: false).CreateE090SubstID(tableObserver: tableObserver));
                default: return null;
            }
        }
    }

    #endregion

    #region SRMConfigBase, SRMBase

    public class SRMConfigBase
    {
        public SRMConfigBase(string partID)
        {
            PartID = partID;
        }

        public SRMConfigBase(SRMConfigBase other)
        {
            PartID = other.PartID;
            AutoLocNameToITPRDictionary = other.AutoLocNameToITPRDictionary.MapNullToEmpty();
            ManualLocNameToITPRDictionary = other.ManualLocNameToITPRDictionary.MapNullToEmpty();
        }

        public string PartID { get; private set; }

        /// <summary>This dictionary gives the set of locations (and ITRP instances) for which material movement operations will automatically perform acquire and release TPR operations.  Items in the manual set but not in this set will not have automatic acquire performed on them during such movement.  Items that are only in this list will be added to the manual list when the SRMBase is constructed.</summary>
        public ReadOnlyIDictionary<string, ITransferPermissionRequest> AutoLocNameToITPRDictionary { get; set; }

        /// <summary>This dictionary gives the set of locations (and ITRP instances) with which TransferPermissionRequestItem can be explicitly used in an SRMBase.  Internally the SRMBase will merge this set with the contents of the AutoLocNameToITPRDictionary so that all automatic locations can also be manually acquired and released.</summary>
        public ReadOnlyIDictionary<string, ITransferPermissionRequest> ManualLocNameToITPRDictionary { get; set; }
    }

    /// <summary>
    /// This is a useful base class for creating usage specific SRM types.  This class incorporates some of the common sequence execution framework logic 
    /// </summary>
    public class SRMBase<TSRMConfigType> : SimpleActivePartBase, ISubstrateRoutingManager
        where TSRMConfigType : SRMConfigBase, ICopyable<TSRMConfigType>
    {
        public SRMBase(TSRMConfigType config, IE039TableUpdater e039TableUpdater, SimpleActivePartBaseSettings ? initialSettings = null)
            : base(config.PartID, initialSettings: initialSettings ?? SimpleActivePartBaseSettings.DefaultVersion2)
        {
            Config = config.MakeCopyOfThis();

            E039TableUpdater = e039TableUpdater;

            AutoLocNameToITPRDictionary = Config.AutoLocNameToITPRDictionary;
            ManualLocNameToITPRDictionary = Config.ManualLocNameToITPRDictionary;

            bool autoITPRDictionaryIsEmpty = Config.AutoLocNameToITPRDictionary.IsNullOrEmpty();
            bool manualITPRDictionaryIsEmpty = Config.ManualLocNameToITPRDictionary.IsNullOrEmpty();

            if (!autoITPRDictionaryIsEmpty && manualITPRDictionaryIsEmpty)
            {
                // auto is non-empty and manual is empty - replace manual with auto so that we can do manual requests for all of the auto items.
                ManualLocNameToITPRDictionary = AutoLocNameToITPRDictionary;
            }
            else if (!autoITPRDictionaryIsEmpty && !manualITPRDictionaryIsEmpty)
            {
                // both auto and manual are provided as non-empty - internally use the union of the auto with the manual set.  the manual set has priority (same name items replaces any one corresponding from the auto set)
                ManualLocNameToITPRDictionary = new ReadOnlyIDictionary<string, ITransferPermissionRequest>(AutoLocNameToITPRDictionary.ConvertToWritable().SafeAddRange(ManualLocNameToITPRDictionary));
            }
        }

        protected TSRMConfigType Config { get; private set; }

        public IE039TableUpdater E039TableUpdater { get; private set; }
        public ReadOnlyIDictionary<string, ITransferPermissionRequest> AutoLocNameToITPRDictionary { get; private set; }
        public ReadOnlyIDictionary<string, ITransferPermissionRequest> ManualLocNameToITPRDictionary { get; private set; }

        /// <summary>Action factory method:  When run, the resulting action will attempt to retract all robot arms and will then release all held ITPR interfaces that this instance knows about.</summary>
        public virtual IClientFacet RetractArmsAndReleaseAll(bool optimizeReleasing = true)
        {
            return new BasicActionImpl(actionQ, ipf => PerformRetractArmsAndReleaseAll(ipf, optimizeReleasing), CurrentMethodName, ActionLoggingReference);
        }

        protected virtual string PerformRetractArmsAndReleaseAll(IProviderFacet ipf, bool optimizeReleasing)
        {
            string ec = PerformRetractArms(ipf);

            if (ec.IsNullOrEmpty())
            {
                var itprPartArray = ManualLocNameToITPRDictionary.Select(kvp => kvp.Value).ToArray();

                if (optimizeReleasing)
                    itprPartArray = itprPartArray.Where(itpr => itpr.TransferPermissionStatePublisher.Object.IsAnyGranted(checkAvailable: false)).ToArray();
                    
                var releaseAllICFArray = itprPartArray.Select(itpr => itpr.TransferPermission(TransferPermissionRequestType.ReleaseAll).StartInline()).ToArray();

                releaseAllICFArray.WaitUntilSetComplete(isWaitLimitReachedDelegate: () => HasStopBeenRequested || ipf.ActionState.IsCancelRequested);
                var releaseAllActionStates = releaseAllICFArray.Select(icf => icf.ActionState);
                var firstFailed = releaseAllActionStates.FirstOrDefault(actionState => actionState.Failed);
                ec = (firstFailed != null) ? firstFailed.ResultCode : string.Empty;
            }

            return ec;
        }

        /// <summary>
        /// The default implementation of this method fails by indicating that it has not been implemented.
        /// </summary>
        protected virtual string PerformRetractArms(IProviderFacet ipf)
        {
            return "{0} has not been implemented in this base class.  It must be overriden in a dervied type.".CheckedFormat(CurrentMethodName);
        }

        /// <summary>Action factory method:  When run, the resulting action will attempt to execute each of the items in the given sequence of items.</summary>
        public virtual IClientFacet Sequence(params SubstrateRoutingItemBase[] itemParamsArray)
        {
            return new BasicActionImpl(actionQ, ipf => PerformSequence(ipf, itemParamsArray.SafeToArray()), CurrentMethodName, ActionLoggingReference, mesgDetails: string.Join(",", itemParamsArray.Select(item => item.ToString())));
        }

        protected virtual string PerformSequence(IProviderFacet ipf, SubstrateRoutingItemBase[] itemArray)
        {
            string ec = string.Empty;

            string endSequenceReason = null;

            foreach (var item in itemArray)
            {
                if (item is MoveSubstrateItem)
                    ec = PerformItem(ipf, (MoveSubstrateItem)item);
                else if (item is SwapSubstratesItem)
                    ec = PerformItem(ipf, (SwapSubstratesItem)item);
                else if (item is MoveOrSwapSubstrateItem)
                    ec = PerformItem(ipf, (MoveOrSwapSubstrateItem)item);
                else if (item is ApproachLocationItem)
                    ec = PerformItem(ipf, (ApproachLocationItem)item);
                else if (item is RunActionItem)
                    ec = PerformItem(ipf, (RunActionItem)item);
                else if (item is TransferPermissionRequestItem)
                    ec = PerformItem(ipf, (TransferPermissionRequestItem)item);
                else if (item is DelegatePredicateItem)
                    ec = PerformItem(ipf, (DelegatePredicateItem)item, out endSequenceReason);
                else
                    ec = "Item type '{0}' is not supported".CheckedFormat(item.GetType());

                if (ec.IsNeitherNullNorEmpty())
                    break;

                if (endSequenceReason.IsNeitherNullNorEmpty())
                {
                    Log.Debug.Emit("Sequence completed early [item '{0}' gave reason '{1}']", item, endSequenceReason);
                    break;
                }
            }

            if (ec.IsNullOrEmpty())
                ec = ReleaseAcquiredEndOfSequenceTransferPermissionsIfNeeded(ipf);

            if (!ec.IsNullOrEmpty())
                NoteSequenceFailed(ipf);

            return ec;
        }

        protected virtual string PerformItem(IProviderFacet ipf, MoveSubstrateItem item)
        {
            UpdateObservers();

            var substID = item.SubstID.MapNullToEmpty();
            var toLocName = item.ToSubstLocName.MapNullToEmpty();

            var desc = "{0}[{1} -> {2}]".CheckedFormat(item.GetType().GetTypeLeafName(), substID.FullName, toLocName);

            var substObs = new E090SubstObserver(substID);

            if (!substObs.Info.IsValid)
                return "{0} failed: given substrate ID is not valid".CheckedFormat(desc);

            var substCurrentLocName = substObs.Info.LocID;
            var fromLocObs = GetSubstLocObserver(substCurrentLocName);
            if (fromLocObs == null)
                return "{0} failed: given substrate's current location is not supported here [{1}]".CheckedFormat(desc, substCurrentLocName);

            if (!fromLocObs.IsAccessible)
                return "{0} failed: given substrate's current location is not accessible [{1}]".CheckedFormat(desc, fromLocObs.Info.NotAccessibleReason);

            var toLocObs = GetSubstLocObserver(toLocName, SubstLocType.EmptyDestination);

            if (toLocObs == null)
                return "{0} failed: given move to location is not supported here [{1}]".CheckedFormat(desc, toLocName);

            if (!toLocObs.IsAccessible)
                return "{0} failed: given move to location is not accessible [{1}]".CheckedFormat(desc, toLocObs.Info.NotAccessibleReason);

            string ec = InnerPerformMoveSubstrate(ipf, substObs, fromLocObs, toLocObs, desc);

            if (ec.IsNullOrEmpty())
                ec = ReleaseAcquiredEndOfItemTransferPermissionsIfNeeded(ipf);

            return ec;
        }

        protected virtual string InnerPerformMoveSubstrate(IProviderFacet ipf, E090SubstObserver substObs, E090SubstLocObserver fromLocObs, E090SubstLocObserver toLocObs, string desc)
        {
            return "{0}.{1} has not been implemented [{2}]".CheckedFormat(CurrentClassLeafName, CurrentMethodName, desc);
        }

        protected virtual string PerformItem(IProviderFacet ipf, SwapSubstratesItem item)
        {
            UpdateObservers();

            var substID = item.SubstID.MapNullToEmpty();
            var swapWithSubstID = item.SwapWithSubstID.MapNullToEmpty();

            var desc = "{0}[{1} with {2}]".CheckedFormat(item.GetType().GetTypeLeafName(), substID.FullName, swapWithSubstID.FullName);

            var substObs = new E090SubstObserver(substID);
            var swapWithSubstObs = new E090SubstObserver(swapWithSubstID);

            if (!substObs.Info.IsValid)
                return "{0} failed: given substrate ID is not valid".CheckedFormat(desc);

            if (!swapWithSubstObs.Info.IsValid)
                return "{0} failed: given swap with substrate ID is not valid".CheckedFormat(desc);

            var substCurrentLocName = substObs.Info.LocID;
            var swapWithSubstCurrentLocName = swapWithSubstObs.Info.LocID;

            var fromLocObs = GetSubstLocObserver(substCurrentLocName);
            var swapAtLocObs = GetSubstLocObserver(swapWithSubstCurrentLocName);

            if (fromLocObs == null)
                return "{0} failed: given substrate's current location is not supported here [{1}]".CheckedFormat(desc, substCurrentLocName);

            if (!fromLocObs.IsAccessible)
                return "{0} failed: given substrate's current location is not accessible [{1}]".CheckedFormat(desc, fromLocObs.Info.NotAccessibleReason);

            if (swapAtLocObs == null)
                return "{0} failed: given swap with substrate's current location is not supported here [{1}]".CheckedFormat(desc, swapWithSubstCurrentLocName);

            if (!swapAtLocObs.IsAccessible)
                return "{0} failed: given swap with substrate's location is not accessible [{1}]".CheckedFormat(desc, swapAtLocObs.Info.NotAccessibleReason);

            string ec = InnerPerformSwapSubstrates(ipf, substObs, fromLocObs, swapWithSubstObs, swapAtLocObs, desc);

            if (ec.IsNullOrEmpty())
                ec = ReleaseAcquiredEndOfItemTransferPermissionsIfNeeded(ipf);

            return ec;
        }

        protected virtual string InnerPerformSwapSubstrates(IProviderFacet ipf, E090SubstObserver substObs, E090SubstLocObserver fromLocObs, E090SubstObserver swapWithSubstObs, E090SubstLocObserver swapAtLocObs, string desc)
        {
            return "{0}.{1} has not been implemented [{2}]".CheckedFormat(CurrentClassLeafName, CurrentMethodName, desc);
        }

        protected virtual string PerformItem(IProviderFacet ipf, MoveOrSwapSubstrateItem item)
        {
            UpdateObservers();

            var substID = item.SubstID.MapNullToEmpty();
            var toLocName = item.ToSubstLocName.MapNullToEmpty();

            var desc = "{0}[{1} -> {2}]".CheckedFormat(item.GetType().GetTypeLeafName(), substID.FullName, toLocName);

            var toLocObs = GetSubstLocObserver(toLocName);

            var substObs = new E090SubstObserver(substID);
            var substCurrentLocName = substObs.Info.LocID;
            var fromLocObs = GetSubstLocObserver(substCurrentLocName);

            if (!substObs.Info.IsValid)
                return "{0} failed: given substrate ID is not valid".CheckedFormat(desc);

            if (fromLocObs == null)
                return "{0} failed: given substrate's current location is not supported here [{1}]".CheckedFormat(desc, substCurrentLocName);

            if (!fromLocObs.IsAccessible)
                return "{0} failed: given substrate's current location is not accessible [{1}]".CheckedFormat(desc, fromLocObs.Info.NotAccessibleReason);

            if (toLocObs == null)
                return "{0} failed: given move to/swap at location is not supported here [{1}]".CheckedFormat(desc, toLocName);

            if (!toLocObs.IsAccessible)
                return "{0} failed: given move to/swap at location is not accessible [{1}]".CheckedFormat(desc, toLocObs.Info.NotAccessibleReason);

            string ec = AcquireLocationTransferPermissionForThisItemIfNeeded(ipf, fromLocObs.ID.Name, toLocObs.ID.Name);

            UpdateObservers();

            if (ec.IsNullOrEmpty())
            {
                if (toLocObs.IsUnoccupied)
                {
                    ec = InnerPerformMoveSubstrate(ipf, substObs, fromLocObs, toLocObs, desc);
                }
                else
                {
                    var swapWithSubstID = toLocObs.ContainsSubstInfo.ObjID;
                    var swapWithSubstObs = new E090SubstObserver(swapWithSubstID);

                    desc = "{0}[{1} with {2} @ {3}]".CheckedFormat(item.GetType().GetTypeLeafName(), substID.FullName, swapWithSubstID.FullName, toLocName);

                    ec = InnerPerformSwapSubstrates(ipf, substObs, fromLocObs, swapWithSubstObs, toLocObs, desc);
                }
            }

            if (ec.IsNullOrEmpty())
                ec = ReleaseAcquiredEndOfItemTransferPermissionsIfNeeded(ipf);

            return ec;
        }

        protected virtual string PerformItem(IProviderFacet ipf, ApproachLocationItem item)
        {
            var desc = item.ToString();

            var toLocName = item.ToSubstLocName;
            E090SubstLocObserver toLocObs = null;

            if (toLocName.IsNullOrEmpty() && !item.SubstID.IsEmpty)
            {
                var substObs = new E090SubstObserver(item.SubstID);

                if (!substObs.Info.IsValid)
                    return "{0} failed: given approach substrate ID is not valid [{1}]".CheckedFormat(desc, item.SubstID);

                toLocName = substObs.Info.LocID;
                toLocObs = GetSubstLocObserver(toLocName);

                if (toLocObs == null)
                    return "{0} failed: given approach substrate is not currently at a supported location [{1}]".CheckedFormat(desc, toLocName);
            }
            else
            {
                toLocObs = GetSubstLocObserver(item.ToSubstLocName);

                if (toLocObs == null)
                    return "{0} failed: given approach to location is not supported here [{1}]".CheckedFormat(desc, toLocName);
            }

            return InnerPerformApproach(ipf, item, toLocObs, desc);
        }

        protected virtual string InnerPerformApproach(IProviderFacet ipf, ApproachLocationItem item, E090SubstLocObserver toLocObs, string desc)
        {
            return "{0}.{1} has not been implemented [{2}]".CheckedFormat(CurrentClassLeafName, CurrentMethodName, desc);
        }

        protected virtual string PerformItem(IProviderFacet ipf, RunActionItem item)
        {
            bool onlyStartAction = item.Behavior.IsSet(RunActionBehaviorFlags.OnlyStartAction);
            bool ignoreFailures = item.Behavior.IsSet(RunActionBehaviorFlags.IgnoreFailures);

            IClientFacet icf = item.CreateICFIfNeeded();
            string ec = string.Empty;

            if (icf != null)
                ec = icf.Start();
            else
                ec = "No IClientFacet was provided";

            if (ec.IsNullOrEmpty() && icf != null && !onlyStartAction)
                ec = WaitForCompletion(ipf, icf);

            if (!ec.IsNullOrEmpty() && ignoreFailures)
            {
                if (icf != null)
                    Log.Debug.Emit("Ignoring failure [icf:{0} ec:{1}]", icf, ec);
                else
                    Log.Debug.Emit("Ignoring failure [ec:{0}]", ec);

                ec = string.Empty;
            }

            return ec;
        }

        protected virtual string PerformItem(IProviderFacet ipf, TransferPermissionRequestItem item)
        {
            var locNameList = (item.LocNameList ?? ReadOnlyIList<string>.Empty); 
            var locNameArray = locNameList.ToArray();
            var kvpArray = locNameList.Select(locName => KVP.Create(locName, ManualLocNameToITPRDictionary.SafeTryGetValue(locName))).ToArray();

            if (kvpArray.Any(kvp => kvp.Value == null))
                return "TransferPermissionRequestItem '{0}' is not supported for given location(s) [{1}]".CheckedFormat(item.Settings, String.Join(", ", kvpArray.Where(kvp => kvp.Value == null).Select(kvp => kvp.Key)));

            string ec = WaitForPostedItemsComplete(ipf, locNameArray);

            var isRecursiveAcquire = item.Settings.IsSet(TransferPermissionRequestItemSettings.RecursiveAcquire);
            var isAcquireIfNeeded = !isRecursiveAcquire && item.Settings.IsSet(TransferPermissionRequestItemSettings.Acquire);
            var autoReleaseAtEndOfSequence = (isAcquireIfNeeded | isRecursiveAcquire) && item.Settings.IsSet(TransferPermissionRequestItemSettings.AutoReleaseAtEndOfSequence);
            var isReleaseIfNeeded = !isAcquireIfNeeded && !isRecursiveAcquire && item.Settings.IsSet(TransferPermissionRequestItemSettings.Release);
            var onlyStartRequest = item.Settings.IsSet(TransferPermissionRequestItemSettings.OnlyStartRequest);

            TransferPermissionRequestType requestType = TransferPermissionRequestType.None;
            if (ec.IsNullOrEmpty())
            {
                if (isAcquireIfNeeded)
                {
                    requestType = TransferPermissionRequestType.Acquire;
                    kvpArray = kvpArray.Where(kvp => !kvp.Value.TransferPermissionStatePublisher.Object.IsGranted(kvp.Key, checkAvailable: false)).ToArray();
                }
                else if (isRecursiveAcquire)
                {
                    requestType = TransferPermissionRequestType.Acquire;
                }
                else if (isReleaseIfNeeded)
                {
                    requestType = TransferPermissionRequestType.Release;
                    kvpArray = kvpArray.Where(kvp => kvp.Value.TransferPermissionStatePublisher.Object.IsGranted(kvp.Key, checkAvailable: false)).ToArray();
                }
                else
                {
                    ec = "Internal: TransferPermissionRequestItem Settings '{0}' unrecognized or unsupported".CheckedFormat(item.Settings);
                }
            }

            if (ec.IsNullOrEmpty())
            {
                if (onlyStartRequest)
                {
                    postedItemList.AddRange(kvpArray.Select(kvp => new PostedItem() { KVP = kvp, ICF = kvp.Value.TransferPermission(requestType, kvp.Key).StartInline() }));
                }
                else
                {
                    var icfArray = kvpArray.Select(kvp => kvp.Value.TransferPermission(requestType, kvp.Key).StartInline()).ToArray();
                    ec = WaitForCompletion(ipf, icfArray);
                }
            }

            // on release remove each item from the currentSequenceAutoReleaseKVPList whose name matches on of the locNames in the 
            if (ec.IsNullOrEmpty() && isReleaseIfNeeded)
            {
                var releasedLocNameArray = kvpArray.Select(kvp => kvp.Key).ToArray();
                currentSequenceAutoReleaseKVPList.FilterAndRemove(kvp => releasedLocNameArray.Contains(kvp.Key));
            }

            if (ec.IsNullOrEmpty() && autoReleaseAtEndOfSequence)
            {
                currentSequenceAutoReleaseKVPList.AddRange(kvpArray);
            }

            return ec;
        }

        protected virtual string PerformItem(IProviderFacet ipf, DelegatePredicateItem item, out string endSequenceReason)
        {
            var desc = item.ToString();

            endSequenceReason = null;

            string predicateResultStr = (item.PredicateDelegate != null) ? item.PredicateDelegate() : "PredicateDelegate is null";
            bool predicateSucceeded = predicateResultStr.IsNullOrEmpty();

            if (!predicateSucceeded && !item.NamedValuesToMergeOnNegativeResult.IsNullOrEmpty())
            {
                ipf.UpdateNamedValues(ipf.ActionState.NamedValues.MergeWith(item.NamedValuesToMergeOnNegativeResult, NamedValueMergeBehavior.AddAndUpdate).MakeReadOnly());
            }

            switch (item.Behavior)
            {
                case PredicateItemBehavior.None:
                    if (predicateSucceeded)
                        Log.Debug.Emit("{0}: predicate succeeded", desc);
                    else
                        Log.Debug.Emit("{0}: predicate failed: {1}", desc, predicateResultStr);

                    return string.Empty;

                case PredicateItemBehavior.NegativeFailsSequence:
                    if (predicateSucceeded)
                        return string.Empty;
                    else
                        return "{0} failed: {1}".CheckedFormat(desc, predicateResultStr);

                case PredicateItemBehavior.NegativeEndsSequence:
                    endSequenceReason = predicateResultStr;

                    return string.Empty;

                default:
                    return "{0} failed: selected Behavior '{1}' is not valid".CheckedFormat(desc, item.Behavior);
            }
        }

        protected virtual string WaitForPostedItemsComplete(IProviderFacet ipf, string[] locNameArray = null)
        {
            string ec = string.Empty;

            if (postedItemList.Count > 0)
            {
                PostedItem [] filteredPostedItemArray = null;

                if (locNameArray != null)
                    filteredPostedItemArray = postedItemList.FilterAndRemove(item => locNameArray.Contains(item.KVP.Key)).ToArray();
                else
                {
                    filteredPostedItemArray = postedItemList.ToArray();
                    postedItemList.Clear();
                }

                ec = WaitForCompletion(ipf, filteredPostedItemArray.Select(item => item.ICF).ToArray());

                if (ec.IsNullOrEmpty())
                    currentSequenceAutoReleaseKVPList.AddRange(filteredPostedItemArray.Select(item => item.KVP));
            }

            return ec;
        }

        protected virtual string AcquireLocationTransferPermissionForThisItemIfNeeded(IProviderFacet ipf, params string[] locNameParamsArray)
        {
            string ec = WaitForPostedItemsComplete(ipf, locNameParamsArray);

            if (ec.IsNullOrEmpty())
            {
                var neededKVPSet = locNameParamsArray.Select(locName => KVP.Create(locName, AutoLocNameToITPRDictionary.SafeTryGetValue(locName))).Where(kvp => kvp.Value != null && !kvp.Value.TransferPermissionStatePublisher.Object.IsGranted(kvp.Key, checkAvailable: false)).ToArray();

                currentItemAutoReleaseKVPList.AddRange(neededKVPSet);      // request each one to get released even if the acquire fails

                var acquireICFArray = neededKVPSet.Select(kvp => kvp.Value.TransferPermission(TransferPermissionRequestType.Acquire, kvp.Key).StartInline()).ToArray();

                ec = WaitForCompletion(ipf, acquireICFArray);
            }

            return ec;
        }

        protected virtual string ReleaseAcquiredEndOfItemTransferPermissionsIfNeeded(IProviderFacet ipf)
        {
            string ec = string.Empty;

            if (currentItemAutoReleaseKVPList.Count > 0)
            {
                var releaseICFs = currentItemAutoReleaseKVPList.Select(kvp => kvp.Value.TransferPermission(TransferPermissionRequestType.Release, kvp.Key).StartInline()).ToArray();
                currentItemAutoReleaseKVPList.Clear();

                ec = WaitForCompletion(ipf, releaseICFs);
            }

            return ec;
        }

        protected virtual string ReleaseAcquiredEndOfSequenceTransferPermissionsIfNeeded(IProviderFacet ipf)
        {
            string ec = WaitForPostedItemsComplete(ipf);

            if (currentItemAutoReleaseKVPList.Count > 0)
            {
                currentSequenceAutoReleaseKVPList.AddRange(currentItemAutoReleaseKVPList);
                currentItemAutoReleaseKVPList.Clear();
            }

            if (currentSequenceAutoReleaseKVPList.Count > 0)
            {
                var releaseICFs = currentSequenceAutoReleaseKVPList.Select(kvp => kvp.Value.TransferPermission(TransferPermissionRequestType.Release, kvp.Key).StartInline()).ToArray();
                currentSequenceAutoReleaseKVPList.Clear();

                ec = WaitForCompletion(ipf, releaseICFs);
            }

            return ec;
        }

        protected virtual void NoteSequenceFailed(IProviderFacet ipf)
        {
            postedItemList.Clear();

            currentItemAutoReleaseKVPList.Clear();
            currentSequenceAutoReleaseKVPList.Clear();
        }

        protected struct PostedItem
        {
            public KeyValuePair<string, ITransferPermissionRequest> KVP { get; set; }
            public IClientFacet ICF { get; set; }
        }

        protected List<PostedItem> postedItemList = new List<PostedItem>();
        protected List<KeyValuePair<string, ITransferPermissionRequest>> currentItemAutoReleaseKVPList = new List<KeyValuePair<string, ITransferPermissionRequest>>();
        protected List<KeyValuePair<string, ITransferPermissionRequest>> currentSequenceAutoReleaseKVPList = new List<KeyValuePair<string, ITransferPermissionRequest>>();

        protected void SetAllSubstLocObservers(IEnumerable<E039ObjectID> substLocIDsSet)
        {
            SetAllSubstLocObservers(substLocIDsSet.Select(locID => new E090SubstLocObserver(locID)));
        }

        protected virtual void SetAllSubstLocObservers(IEnumerable<E090SubstLocObserver> substLocObserverSet)
        {
            allSubstLocObserverArray = substLocObserverSet.ToArray();
            allSubstLocObserverArray.DoForEach(obs => { allSubstLocObserverByLocNameDictionary[obs.ID.Name] = obs; });
        }

        protected E090SubstLocObserver[] allSubstLocObserverArray = EmptyArrayFactory<E090SubstLocObserver>.Instance;
        protected IDictionaryWithCachedArrays<string, E090SubstLocObserver> allSubstLocObserverByLocNameDictionary = new IDictionaryWithCachedArrays<string, E090SubstLocObserver>();

        protected enum SubstLocType : int 
        {
            Normal,
            EmptyDestination,
        }

        protected virtual E090SubstLocObserver GetSubstLocObserver(string locName, SubstLocType locType = SubstLocType.Normal)
        {
            return allSubstLocObserverByLocNameDictionary.SafeTryGetValue(locName);
        }

        protected virtual void UpdateObservers()
        {
            allSubstLocObserverArray.DoForEach(obs => obs.Update());
        }

        protected virtual string WaitForCompletion(IProviderFacet ipf, params IClientFacet[] icfParamsArray)
        {
            if (icfParamsArray.IsNullOrEmpty())
                return string.Empty;

            icfParamsArray.DoForEach(icf => icf.NotifyOnComplete.AddItem(this));

            string ec = string.Empty;

            for (; ;)
            {
                WaitForSomethingToDo();

                if (icfParamsArray.All(icf => icf.ActionState.IsComplete))
                    break;

                if (HasStopBeenRequested)
                {
                    ec = "Part has been asked to stop";
                    icfParamsArray.DoForEach(icf => icf.RequestCancel());
                    break;
                }

                bool cancelRequest = (ipf != null && ipf.IsCancelRequestActive) || (CurrentAction != null && CurrentAction.IsCancelRequestActive);
                if (cancelRequest && !icfParamsArray.All(icf => icf.IsCancelRequestActive))
                    icfParamsArray.DoForEach(icf => icf.RequestCancel());
            }

            icfParamsArray.DoForEach(icf => icf.NotifyOnComplete.RemoveItem(this));

            if (ec.IsNullOrEmpty())
            {
                var firstFailedICF = icfParamsArray.FirstOrDefault(icf => icf.ActionState.Failed);
                ec = (firstFailedICF != null) ? firstFailedICF.ActionState.ResultCode : string.Empty;
            }

            return ec;
        }
    }
    
    #endregion
}
