//-------------------------------------------------------------------
/*! @file E039Accumulators.cs
 *  @brief 
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2020 Mosaic Systems Inc.
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
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Interconnect;
using MosaicLib.Modular.Interconnect.Sets;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Modular.Persist;
using MosaicLib.Modular.Reflection.Attributes;
using MosaicLib.Semi;
using MosaicLib.Semi.E039.Details;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Utils.StringMatching;

namespace MosaicLib.Semi.E039.Accumulators
{
    #region Constants and Settings

    /// <summary>
    /// Static class used as namespace for Accumulator specific global constant values.  (primarily string constants such as type names)
    /// </summary>
    public static class Constants
    {
        /// <summary>DefaultAccumulatorObjectType = "Accumulator"</summary>
        public const string DefaultAccumulatorObjectType = "Accumulator";

        /// <summary>E039 attribute name used to contain the value of the accumulator.</summary>
        public const string ValueAttributeName = "Value";

        /// <summary>E039 attribute name used to contain the optional Range1 lower limit for the accumulator value. [Range1.Low]</summary>
        public const string Range1LowAttributeName = "Range1.Low";

        /// <summary>E039 attribute name used to contain the optional Range1 upper limit for the accumulator value. [Range1.High]</summary>
        public const string Range1HighAttributeName = "Range1.High";

        /// <summary>E039 attribute name used to contain the optional Range2 lower limit for the accumulator value. [Range2.Low]</summary>
        public const string Range2LowAttributeName = "Range2.Low";

        /// <summary>E039 attribute name used to contain the optional Range2 upper limit for the accumulator value. [Range2.High]</summary>
        public const string Range2HighAttributeName = "Range2.High";
    }

    /// <summary>
    /// Static class that is used to hold static (global) settings that are used by the E039 Accumulator specific extension methods to configure their behavior.  
    /// The choice of these setting values are client code specific and are synchronized with correct operation of client code.  
    /// As such there is not built in logic that can be used to obtain their values from modular config.
    /// </summary>
    public static class Settings
    {
        /// <summary>Static get/set property used to define the base set of flags that are used when creating an E039 Accumulator object using this code.  [None]</summary>
        public static E039ObjectFlags DefaultAddObjectFlags { get { return _defaultAddObjectFlags & (E039ObjectFlags.ClientUsableFlags); } set { _defaultAddObjectFlags = value; } }
        private static E039ObjectFlags _defaultAddObjectFlags = E039ObjectFlags.None;

        /// <summary>Gives the default LogConfigSelect value used with E039.Update when the EM does not explicitly provide a value. [null]</summary>
        public static string DefaultIncrementLogConfigSelect { get; set; }

        /// <summary>Restores these settings to their default values</summary>
        public static void ResetToDefaults()
        {
            DefaultAddObjectFlags = E039ObjectFlags.None;
            DefaultIncrementLogConfigSelect = null;
        }
    }
    
    #endregion

    #region E039AccumulatorInfo and E039AccumulatorObserver

    /// <summary>
    /// This is a helper object that is generally used to extract and interpret accumulator related information from an Accumulator type IE039Object.
    /// </summary>
    public struct E039AccumulatorInfo : IEquatable<E039AccumulatorInfo>
    {
        /// <summary>Implicit cast operator to support use implicit conversion of a E039AccumulatorInfo object to the Accumulator ObjID that it was dervied from.</summary>
        public static implicit operator E039ObjectID(E039AccumulatorInfo info) { return info.ObjID; }

        /// <summary>Normal constructor.  Caller must provide a non-empty Accumulator <paramref name="obj"/> [Accumulator]</summary>
        public E039AccumulatorInfo(IE039Object obj)
            : this()
        {
            Obj = obj;
            bool objIsNotNull = (Obj != null);
            ObjID = objIsNotNull ? Obj.ID : null;

            INamedValueSet attributes = objIsNotNull ? Obj.Attributes : NamedValueSet.Empty;

            Value = attributes[Constants.ValueAttributeName].VC;
            Range1 = new VCRange()
            {
                Low = attributes[Constants.Range1LowAttributeName].VC,
                High = attributes[Constants.Range1HighAttributeName].VC
            };
            Range2 = new VCRange()
            {
                Low = attributes[Constants.Range2LowAttributeName].VC,
                High = attributes[Constants.Range2HighAttributeName].VC
            };
        }

        /// <summary>Copy constructor</summary>
        public E039AccumulatorInfo(E039AccumulatorInfo other)
            : this()
        {
            Obj = other.Obj;
            ObjID = other.ObjID;

            Value = other.Value;
            Range1 = other.Range1;
            Range2 = other.Range2;
        }

        /// <summary>Used to update the given <paramref name="nvs"/> to contain Attribute values for the properties in this object that represent Attribute values.</summary>
        public NamedValueSet UpdateAttributeValues(NamedValueSet nvs)
        {
            nvs.ConditionalSetValue(Constants.ValueAttributeName, !Value.IsEmpty, Value);
            if (!Range1.IsNullOrEmpty)
            {
                nvs.SetValue(Constants.Range1LowAttributeName, Range1.Low);
                nvs.SetValue(Constants.Range1HighAttributeName, Range1.High);
            }
            if (!Range2.IsNullOrEmpty)
            {
                nvs.SetValue(Constants.Range2LowAttributeName, Range2.Low);
                nvs.SetValue(Constants.Range2HighAttributeName, Range2.High);
            }

            return nvs;
        }

        /// <summary>Gives the original object that this info object was constructed from, or null for the default constructor.</summary>
        public IE039Object Obj { get; private set; }

        /// <summary>Gives the E039ObjectID of the object from which this structure was created, or E039ObjectID.Empty if the default constructor was used.</summary>
        public E039ObjectID ObjID { get { return _objID ?? E039ObjectID.Empty; } set { _objID = value; } }
        private E039ObjectID _objID;

        /// <summary>Extracted Value from the corresponding attribute</summary>
        public ValueContainer Value { get; set; }

        /// <summary>Extracted Range1 from the corresponding pair of attributes</summary>
        public VCRange Range1 { get; set; }

        /// <summary>Extracted Range2 from the corresponding pair of attributes</summary>
        public VCRange Range2 { get; set; }

        /// <summary>Returns true if the Value attribute is neither null nor empty</summary>
        public bool IsValid { get { return !Value.IsNullOrEmpty; } }

        /// <summary>Gives an empty E090SubstInfo object to be used as a default value.</summary>
        public static E039AccumulatorInfo Empty { get { return default(E039AccumulatorInfo); } }

        /// <summary>Returns true if the contents are the same as the contents of the Empty E039AccumulatorInfo object.</summary>
        public bool IsEmpty { get { return this.Equals(Empty); } }

        /// <summary>
        /// Returns true if the Value is within the specified Range1 range (Range1.IsInRange(Value))
        /// </summary>
        public bool IsInRange1 { get { return Range1.IsInRange(Value); } }

        /// <summary>
        /// Returns true if the Value is within the specified Range2 range (Range2.IsInRange(Value))
        /// </summary>
        public bool IsInRange2 { get { return Range2.IsInRange(Value); } }

        /// <summary>
        /// Returns true if the contents of this object match the contentents of the given <paramref name="other"/> object.
        /// <para/>NOTE: this test does not look at the contents of the Obj property.
        /// </summary>
        public bool Equals(E039AccumulatorInfo other)
        {
            return (ObjID.Equals(other.ObjID)
                    && Value.Equals(other.Value)
                    && Range1.Equals(other.Range1)
                    && Range2.Equals(other.Range2)
                    );
        }

        /// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            return ToString(includeObjID: true);
        }

        /// <summary>Debugging and logging helper</summary>
        public string ToString(bool includeObjID)
        {
            if (IsEmpty)
                return "E039AccumulatorInfo [Empty]";

            StringBuilder sb = new StringBuilder("E039AccumulatorInfo");

            if (includeObjID)
                sb.CheckedAppendFormat(" {0}", ObjID.ToString(E039ToStringSelect.FullName));

            sb.CheckedAppendFormat(" {0}", Value);

            if (!Range1.IsNullOrEmpty)
                sb.CheckedAppendFormat(" Range1:{0}{1}", Range1, IsInRange1 ? " InRange" : "");

            if (!Range2.IsNullOrEmpty)
                sb.CheckedAppendFormat(" Range2:{0}{1}", Range2, IsInRange2 ? " InRange" : "");

            return sb.ToString();
        }
    }

    /// <summary>
    /// Observer helper object for use with Accumulator object publishers
    /// </summary>
    public class E039AccumulatorObserver : E039.E039ObjectObserverWithInfoExtraction<E039AccumulatorInfo>
    {
        /// <summary>Implicit cast operator to support implicit conversion of a E039AccumulatorObserver object to the Accumulator ObjID that it observes.  This implicit cast does not call UpdateOnCastIfNeeded</summary>
        public static implicit operator E039ObjectID(E039AccumulatorObserver accumObs) { return accumObs.ID; }

        /// <summary>Implicit cast operator to support implicit conversion of a E039AccumulatorObserver object to the E039AccumulatorInfo for the Accumulator object that it observes.</summary>
        public static implicit operator E039AccumulatorInfo(E039AccumulatorObserver accumObs) { accumObs.UpdateOnCastIfNeeded(); return accumObs.Info; }

        /// <summary>Normal constructor.  Caller provides a <paramref name="accumID"/>.  This method calls GetPublisher on it and then initializes itself using the obtained object publisher.</summary>
        public E039AccumulatorObserver(E039ObjectID accumID)
            : this(accumID.GetPublisher())
        { }

        /// <summary>Normal constructor.  Caller provides the object publisher instance to observe from.</summary>
        public E039AccumulatorObserver(ISequencedObjectSource<IE039Object, int> objPublisher)
            : base(objPublisher, (obj) => new E039AccumulatorInfo(obj))
        { }

        /// <summary>
        /// "Copy" constructor.  
        /// <para/>Note: this constructor implicitly Updates the constructed observer so it may not give the same property values (Info, Object, ID) if a new object has been published since the <paramref name="other"/> observer was last Updated.
        /// <para/>Note: this copy constructor does not copy the <paramref name="other"/>'s UpdateAndGetObjectUpdateActionArray.  Any desired object update actions for this new observer must be added explicitly.
        /// </summary>
        public E039AccumulatorObserver(E039AccumulatorObserver other)
            : this((other != null) ? other.ObjPublisher : null)
        { }

        /// <summary>
        /// Debug and logging helper method
        /// </summary>
        public override string ToString()
        {
            return "AccumObs: {0}{1}".CheckedFormat(Info, IsUpdateNeeded ? " UpdateNeeded" : "");
        }
    }

    #endregion

    #region E039AccumulatorSourceHelper, E039AccumulatorTotalizingSourceHelper

    /// <summary>
    /// This helper class provides a standard set of functionality to be used with clients that accumulate a measured value at a rate that is higher than the systme would generally
    /// like to publiish and persist via the E039 Table Manager.  
    /// This helper class may be constructed with an UpdateHoldoffPeriod that defines the nominal maximum rate that it will update the E039 table object.  
    /// If the client Increments the helper at a rate that is higher than this then the helper will accumulate the given values into its PendingIncrement property until the next
    /// update holdoff period has elapsed and then it will push the accumulated pending increment to the accumulator object the standard accumulator Increment pattern.
    /// This allows a client to call Increment frequently with small (or zero) values without causing a correspondingly high rate of actuall e039 Update operations.
    /// The client may use the AdjustedValue and/or AdjustedInfo properties to observe the locally adjusted Value/Info contents which will give the last serviced Info.Value/Info 
    /// with the current PendingIncrement added to it.  This allows the client to make decisions on the sum of the pushed and pending values including using the InRange1 and InRange2 Info properties.
    /// <para/>NOTE: The client should periodially call Service if the client does not call Increment on a pure time basis.
    /// <para/>NOTE: As the contents of the ActiveANNames property are managed elsewhere, the AdjustedInfo will not report changes in the ActiveANNames based InRange changes that are based on unpushed increments directly.
    /// Client code that wishes to implement programatic use of ranges with local accumulated increment values should either use the AdjustedInfo InRange1/InRange2 properties directly or 
    /// should select the use of the forcePush option on the Service method before making a critical decision so that the helper instance will have been synchronized with the table contents
    /// immediately before the decision is made.
    /// <para/>WARNING: if this tool is used in cases where more than one code path/client may increment the same accumulator ID, the client must be aware that the 
    /// holdoff logic used here will mean that the AdjustedValue and AdjustedInfo will only reflect externally applied changes immediately after using the Service or Increment methods.
    /// </summary>
    public class E039AccumulatorSourceHelper : IServiceable
    {
        /// <summary>Defines the default UpdateHoldoffPeriod used by newly constructed instances if they do not explicitly specify a value [0.333 seconds]</summary>
        public static TimeSpan DefaultUpdateHoldoffPeriod = (0.333).FromSeconds();
        /// <summary>Defines the default E039LogConfigSelect for newly constructed instances if they do not explicitly specify a value ["Accumulator"]</summary>
        public static string DefaultE039LogConfigSelect = "Accumulator";
        /// <summary>Defines the initial AllowUpcastAttempts property value for newly constucted instances. [true]</summary>
        public static bool DefaultAllowUpcastAttempts = true;
        /// <summary>Defines the initial Rethrow property value for newly constructed instances. [false]</summary>
        public static bool DefaultRethrow = false;
        /// <summary>Defines the initial ClearPendingIncrementOnAccumulatorCleared property value for newly constructed instances. [true]</summary>
        public static bool DefaultClearPendingIncrementOnAccumulatorCleared = true;

        /// <summary>
        /// Constructor.  
        /// Caller must provide the <paramref name="accumID"/> accumulator ID which is used to obtain a publisher that can be observed.  
        /// Caller may provide a non-default <paramref name="updateHoldoffPeriod"/> and <paramref name="e039LogConfigSelect"/>
        /// </summary>
        public E039AccumulatorSourceHelper(E039ObjectID accumID, TimeSpan? updateHoldoffPeriod = null, string e039LogConfigSelect = null)
            :this(new E039AccumulatorObserver(accumID), updateHoldoffPeriod: updateHoldoffPeriod, e039LogConfigSelect: e039LogConfigSelect)
        { }

        /// <summary>
        /// Internal constructor used when this logic knows that it is the only place where the given <paramref name="accumObs"/> instance will be retained or used.
        /// </summary>
        internal E039AccumulatorSourceHelper(E039AccumulatorObserver accumObs, TimeSpan? updateHoldoffPeriod = null, string e039LogConfigSelect = null)
        {
            observer = accumObs;

            UpdateHoldoffPeriod = updateHoldoffPeriod ?? DefaultUpdateHoldoffPeriod;
            if (!UpdateHoldoffPeriod.IsZero())
                updateHoldoffTimer = new QpcTimer() { TriggerInterval = UpdateHoldoffPeriod, AutoReset = true}.Start();

            E039LogConfigSelect = e039LogConfigSelect ?? DefaultE039LogConfigSelect;
            AllowUpcastAttempts = DefaultAllowUpcastAttempts;
            Rethrow = DefaultRethrow;
            ClearPendingIncrementOnAccumulatorCleared = DefaultClearPendingIncrementOnAccumulatorCleared;
        }

        private E039AccumulatorObserver observer;

        /// <summary>Gives the E039ObjectID of the accumulator that this helper is observing and is able to increment.</summary>
        public E039ObjectID ID { get { return observer.ID; } }

        /// <summary>Gives the nominial holdoff period that this instance has been configured to use.</summary>
        public TimeSpan UpdateHoldoffPeriod { get; private set; }
        QpcTimer updateHoldoffTimer;

        /// <summary>Gives the E039 Update logConfigSelect value that this inistance has been configured to use.</summary>
        public string E039LogConfigSelect { get; private set; }

        /// <summary>Determines if this instance will allow upcast attempts when performing local or e039 table increment operations.</summary>
        public bool AllowUpcastAttempts { get; set; }

        /// <summary>Determines the rethrow: value that will be used by this instance when performing local ValueContainer operations (Sum).</summary>
        public bool Rethrow { get; set; }

        /// <summary>Determines if this instance will monitor for external updates to the observed accumulator value and clear the local PendingIncrement when the external Value transitions to a new value that is Zero, None or Null (aka of some external entity cleared it).</summary>
        public bool ClearPendingIncrementOnAccumulatorCleared { get; set; }

        /// <summary>
        /// Increments either the PendingIncrement or the actual accumulator's Value based on the elapsed time since the last push operation and the value of the given <paramref name="pushNow"/> flag.
        /// This method implicitly checks for the accumulator being cleared if this instance has that feature enabled.
        /// </summary>
        public void Increment(ValueContainer value, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp), bool pushNow = false)
        {
            InnerAddAndUpdateIfNeeded(value, qpcTimeStamp, allowAutoClear: true, pushNow: pushNow);
        }

        /// <summary>Gives the current accumulated pending increment value.  This will be Empty after a Push has been completed and will be non-Empty when non-zero increments are added to it when they are not otherwise immediately pushed.</summary>
        public ValueContainer PendingIncrement { get; set; }

        int IServiceable.Service(QpcTimeStamp qpcTimeStamp)
        {
            return this.Service(qpcTimeStamp: qpcTimeStamp, pushNow: false, forceUpdate: false);
        }

        /// <summary>
        /// Services the instance, checking for a non-Empty PendingIncrement value and then pushing the pending value to the accumulator object if the pending holdoff timer has expiered or <paramref name="pushNow"/> is true.
        /// This method implicitly checks for the accumulator being cleared if this instance has that feature enabled.
        /// <para/>NOTE: The client should periodially call Service if the client does not call Increment on a pure time basis.
        /// </summary>
        public virtual int Service(QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp), bool pushNow = false, bool forceUpdate = false)
        {
            if (PendingIncrement.IsNeitherNullNorEmpty())
                return InnerAddAndUpdateIfNeeded(ValueContainer.Empty, qpcTimeStamp, allowAutoClear: true, pushNow: pushNow);
            else
                return InnerUpdateObserverIfNeeded(allowAutoClear: true, forceUpdate: forceUpdate);
        }

        /// <summary>
        /// Clears the accumulator (by setting its value to Zero in the current numeric value representation or to ValueContainer.Empty otherwise.  Also clears the PendingIncrement.
        /// </summary>
        public void Clear(bool resetUpdateTimer = true)
        {
            observer.Update();      // in case we do not have a valid Value yet.

            observer.Clear(update: true, logConfigSelect: E039LogConfigSelect);

            PendingIncrement = ValueContainer.Empty;

            if (resetUpdateTimer)
                updateHoldoffTimer.Reset();
        }

        /// <summary>
        /// Allows the caller to set the <paramref name="value"/> and/or the range values. Also clears the PendingIncrement if <paramref name="value"/> is not null.
        /// </summary>
        public void SetValues(ValueContainer? value = null, VCRange? range1 = null, VCRange? range2 = null, bool resetUpdateTimer = true)
        {
            observer.SetValues(value: value, range1: range1, range2: range2, update: true, logConfigSelect: E039LogConfigSelect);

            if (value != null)
            {
                PendingIncrement = ValueContainer.Empty;

                if (resetUpdateTimer)
                    updateHoldoffTimer.Reset();
            }
        }

        private int InnerAddAndUpdateIfNeeded(ValueContainer incrementValue, QpcTimeStamp qpcTimeStamp, bool allowAutoClear = true, bool pushNow = false)
        {
            if (observer.IsUpdateNeeded && !pushNow)
                InnerUpdateObserverIfNeeded(allowAutoClear: allowAutoClear, forceUpdate: true);

            bool pendingIncrementIsEmpty = PendingIncrement.IsEmpty;
            bool incrementValueIsNullOrEmptyOrZero = incrementValue.IsNullOrEmptyOrZero();

            if (pendingIncrementIsEmpty && incrementValueIsNullOrEmptyOrZero)
            {
                if (pushNow)
                    InnerUpdateObserverIfNeeded(allowAutoClear: allowAutoClear);

                return 0;
            }

            bool doPush = pushNow || updateHoldoffTimer.GetIsTriggered(qpcTimeStamp.MapDefaultToNow());

            if (doPush)
            {
                if (incrementValueIsNullOrEmptyOrZero)
                    incrementValue = PendingIncrement;
                else
                    incrementValue = PendingIncrement.Sum(incrementValue, allowUpcastAttempts: AllowUpcastAttempts, rethrow: Rethrow);

                PendingIncrement = ValueContainer.Empty;

                observer.Increment(incrementValue, enableUpcasts: AllowUpcastAttempts, update: true, logConfigSelect: E039LogConfigSelect);

                InnerUpdateObserverIfNeeded(allowAutoClear: true, forceUpdate: true);

                return 1;
            }
            else
            {
                // push was not requested and timer did not trigger - InnerUpdateObserverIfNeeded was already run above if needed)

                if (pendingIncrementIsEmpty)
                    PendingIncrement = incrementValue;
                else if (!incrementValueIsNullOrEmptyOrZero)
                    PendingIncrement = PendingIncrement.Sum(incrementValue, allowUpcastAttempts: AllowUpcastAttempts, rethrow: Rethrow);

                return 0;
            }
        }

        private int InnerUpdateObserverIfNeeded(bool allowAutoClear, bool forceUpdate = false)
        {
            if (observer.IsUpdateNeeded || forceUpdate)
            {
                if (allowAutoClear && ClearPendingIncrementOnAccumulatorCleared)
                {
                    ValueContainer entryValue = observer.Info.Value;
                    if (observer.Update())
                    {
                        ValueContainer nextValue = observer.Info.Value;
                        if (!nextValue.Equals(entryValue) && nextValue.IsNullOrEmptyOrZero())
                            PendingIncrement = ValueContainer.Empty;

                        return 1;
                    }
                    return 0;
                }
                else
                {
                    return observer.Update().MapToInt();
                }
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// When queried, this property generates and returns an adjusted Value that includes any local partially accumulated value that has not yet been published to the E039 table manager added to the last observed value (as updated in the last Service call).
        /// <para/>Note: this method does not update the Observer directly.  The client must 
        /// </summary>
        public ValueContainer AdjustedValue { get { return AdjustValue(observer.Info.Value, PendingIncrement); } }

        /// <summary>
        /// When queried, this property generates and returns an adjusted E039AccumulatorInfo value that includes any partially accumulated value that has not yet been published to the E039 table manager.
        /// <para/>Note: the updated Info's Value will contain the sum of the last published Value and the current PendingIncrement
        /// </summary>
        public E039AccumulatorInfo AdjustedInfo { get { return AdjustInfo(observer.Info, PendingIncrement); } }

        private E039AccumulatorInfo AdjustInfo(E039AccumulatorInfo info, ValueContainer adjustment)
        {
            if (adjustment.IsNeitherNullNorEmpty())
                info.Value = AdjustValue(info.Value, adjustment);
            return info;
        }

        private ValueContainer AdjustValue(ValueContainer value, ValueContainer adjustment)
        {
            if (adjustment.IsNeitherNullNorEmpty())
                return adjustment.Sum(value, allowUpcastAttempts: AllowUpcastAttempts, rethrow: Rethrow);
            else
                return value;
        }
    }

    /// <summary>
    /// This class is derived from E039AccumulatorSourceHelper and applies the following changes:
    /// A) It is implicitly only used with F8 values and provides some F8 specific versions of the normal accumulator properties.
    /// B) It adds a F8 ValueToTotalize property and a new ServiceWithValueToTotalize variant that allows the caller to combine the user of Service with updating the value to be totalized
    /// </summary>
    public class E039AccumulatorTotalizingSourceHelper : E039AccumulatorSourceHelper
    {
        /// <summary>Defines the default MaximumInterServicePeriod used by newly constructed instances if they do not explicitly specify a value [10.0 seconds]</summary>
        public static TimeSpan DefaultMaximumInterServicePeriod = (10.0).FromSeconds();

        private static readonly Logging.ILogger classLogger = new Logging.Logger(Fcns.CurrentClassLeafName);

        /// <summary>
        /// Constructor.  
        /// Caller must provide the <paramref name="accumID"/> accumulator ID which is used to obtain a publisher that can be observed.  
        /// Caller may provide a non-default <paramref name="updateHoldoffPeriod"/> and <paramref name="e039LogConfigSelect"/>
        /// </summary>
        public E039AccumulatorTotalizingSourceHelper(E039ObjectID accumID, TimeSpan? updateHoldoffPeriod = null, string e039LogConfigSelect = null)
            : this(new E039AccumulatorObserver(accumID), updateHoldoffPeriod: updateHoldoffPeriod, e039LogConfigSelect: e039LogConfigSelect)
        { }

        /// <summary>
        /// Internal constructor used when this logic knows that it is the only place where the given <paramref name="accumObs"/> instance will be retained or used.
        /// </summary>
        internal E039AccumulatorTotalizingSourceHelper(E039AccumulatorObserver accumObs, TimeSpan? updateHoldoffPeriod = null, string e039LogConfigSelect = null)
            : base(accumObs: accumObs, updateHoldoffPeriod: updateHoldoffPeriod, e039LogConfigSelect: e039LogConfigSelect)
        {
            MaximumInterServicePeriod = DefaultMaximumInterServicePeriod;
        }

        /// <summary>
        /// This property defines the maximum period that can elapse between any pair of Service calls that are used to update the totalizer value so as to prevent a single service call from making a huge change in the totalized value.
        /// When the actual time between service calls exceeds this period then this period will be used as the integration period rather than the actual measured period.
        /// </summary>
        public TimeSpan MaximumInterServicePeriod { get; set; }

        /// <summary>
        /// Gives the value to totalize as last explicitly updated (for use with normal Service method) or as updated by last call to ServiceWithValueToTotalize.
        /// </summary>
        public double ValueToTotalize { get; set; }

        /// <summary>
        /// Gives the timestamp 
        /// </summary>
        private QpcTimeStamp lastServiceTimeStamp = default(QpcTimeStamp);
        private int sequentialExceededMessageCount = 0;

        /// <summary>
        /// Updates the ValueToTotalize with the given value and then services this helper instance to perform the incremental totalizing with the updated value.
        /// </summary>
        public void ServiceWithValueToTotalize(double valueToTotalize, QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp), bool pushNow = false, bool forceUpdate = false)
        {
            ValueToTotalize = valueToTotalize;

            Service(qpcTimeStamp: qpcTimeStamp, pushNow: pushNow, forceUpdate: forceUpdate);
        }

        /// <summary>
        /// Services the instance and implements the totalizing logic.  
        /// First updates the inter service elapsed time calculation. Then if the ValueToTotalize is not zero then calculates the incremental Increment to be applied to the accumulator and calls the base class Increment method to perform the addition.
        /// Otherwise just services the helper using the base class's Service method which checks for a non-Empty PendingIncrement value and then pushs the pending value to the accumulator object if the pending holdoff timer has expiered or <paramref name="pushNow"/> is true.
        /// This method implicitly checks for the accumulator being cleared if this instance has that feature enabled.
        /// <para/>NOTE: The client should periodially call Service if the client does not call ServiceWithValueToTotalize on a pure time basis.
        /// </summary>
        public override int Service(QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp), bool pushNow = false, bool forceUpdate = false)
        {
            qpcTimeStamp = qpcTimeStamp.MapDefaultToNow();
            var lastServiceTimeStampAge = (lastServiceTimeStamp.IsZero ? TimeSpan.Zero : qpcTimeStamp - lastServiceTimeStamp);
            lastServiceTimeStamp = qpcTimeStamp;

            if (ValueToTotalize != 0.0)
            {
                if (lastServiceTimeStampAge > MaximumInterServicePeriod)
                {
                    if (sequentialExceededMessageCount < 3)
                    {
                        sequentialExceededMessageCount++;
                        classLogger.Debug.Emit("{0}: MaximumInterServicePeriod exceeded [{1} > {2} seconds, sequential count:{3}]", ID.FullName, lastServiceTimeStampAge.TotalSeconds, MaximumInterServicePeriod.TotalSeconds, sequentialExceededMessageCount);
                    }

                    lastServiceTimeStampAge = MaximumInterServicePeriod;
                }
                else
                {
                    sequentialExceededMessageCount = 0;
                }

                base.Increment(ValueContainer.CreateF8(ValueToTotalize * lastServiceTimeStampAge.TotalSeconds), qpcTimeStamp: qpcTimeStamp, pushNow: pushNow);

                return 1;
            }
            else
            {
                return base.Service(qpcTimeStamp, pushNow, forceUpdate);
            }
        }

        /// <summary>
        /// Gets and returns the totalized AdjustedValue as an F8.
        /// </summary>
        public double AdjustedTotalizedValue { get { return AdjustedValue.GetValueF8(rethrow: Rethrow); } }
    }

    #endregion

    #region ExtensionMethods

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        #region Accumulator Creation EMs (CreateAccumulatorID, CreateAccumulatorAndTotalizingHelper, CreateAccumulatorAndHelper, CreateAccumulatorAndObserver, GenerateCreateAccumulatorUpdateItem)

        /// <summary>
        /// Creates and returns an E039ObjectId for the given <paramref name="accumulatorName"/>.  The id's TableObserver will be assigned to the given <paramref name="tableObserver"/> value.
        /// The <paramref name="accumulatorObjectType"/> parameter may be overriden to change the object ID Type that is generated. This parameter defaults to Constants.DefaultAccumulatorObjectType [Accumulator]
        /// </summary>
        public static E039ObjectID CreateAccumulatorID(this IE039TableObserver tableObserver, string accumulatorName, string accumulatorObjectType = Constants.DefaultAccumulatorObjectType)
        {
            return new E039ObjectID(accumulatorName, accumulatorObjectType, assignUUID: false, tableObserver: tableObserver);
        }

        /// <summary>
        /// Construction helper method - This method combines accumulator construction (when needed) with creation of a corresponding totalizer helper.
        /// The behavior of the ifNeeded object creation is determined by the GenerateCreateAccumulatorUpdateItem method which makes use of the GenerateAccumulatorAddItemAttributes(<paramref name="initialAttributes"/>, <paramref name="initialInfo"/>, <paramref name="initialValue"/>) method.
        /// <para/>If the initial value defined by the given attribute related parameters is not an F8 then this method will first attempt to cast it to an F8 and, if that fails, will replace the initial value with [F8 0.0].
        /// </summary>
        public static E039AccumulatorTotalizingSourceHelper CreateAccumulatorAndTotalizingHelper(this IE039TableUpdater tableUpdater, string accumulatorName, TimeSpan? updateHoldoffPeriod = null, string e039LogConfigSelect = null, INamedValueSet initialAttributes = null, E039AccumulatorInfo? initialInfo = null, ValueContainer initialValue = default(ValueContainer), string accumulatorObjectType = Constants.DefaultAccumulatorObjectType, E039ObjectFlags flags = E039ObjectFlags.None)
        {
            var addItemAttributes = GenerateAccumulatorAddItemAttributes(initialAttributes, initialInfo, initialValue);

            var valueVC = addItemAttributes[Constants.ValueAttributeName].VC;
            if (valueVC.cvt != ContainerStorageType.F8)
            {
                valueVC = valueVC.Cast(ContainerStorageType.F8, allowTypeChangeAttempt: true);
                if (valueVC.cvt != ContainerStorageType.F8)
                    valueVC = ValueContainer.CreateF8(0.0);

                addItemAttributes = addItemAttributes.ConvertToWritable().SetValue(Constants.ValueAttributeName, valueVC).MakeReadOnly();
            }

            E039AccumulatorObserver accumObs;
            tableUpdater.CreateAccumulatorAndObserver(accumulatorName, out accumObs, initialAttributes: addItemAttributes, initialInfo: null, initialValue: ValueContainer.Empty, accumulatorObjectType: accumulatorObjectType, flags: flags);

            return new E039AccumulatorTotalizingSourceHelper(accumObs, updateHoldoffPeriod: updateHoldoffPeriod, e039LogConfigSelect: e039LogConfigSelect);
        }

        /// <summary>
        /// Construction helper method - This method combines accumulator construction (when needed) with creation of a corresponding helper.
        /// The behavior of the ifNeeded object creation is determined by the GenerateCreateAccumulatorUpdateItem method which makes use of the GenerateAccumulatorAddItemAttributes(<paramref name="initialAttributes"/>, <paramref name="initialInfo"/>, <paramref name="initialValue"/>) method.
        /// </summary>
        public static E039AccumulatorSourceHelper CreateAccumulatorAndHelper(this IE039TableUpdater tableUpdater, string accumulatorName, TimeSpan? updateHoldoffPeriod = null, string e039LogConfigSelect = null, INamedValueSet initialAttributes = null, E039AccumulatorInfo? initialInfo = null, ValueContainer initialValue = default(ValueContainer), string accumulatorObjectType = Constants.DefaultAccumulatorObjectType, E039ObjectFlags flags = E039ObjectFlags.None)
        {
            E039AccumulatorObserver accumObs;
            tableUpdater.CreateAccumulatorAndObserver(accumulatorName, out accumObs, initialAttributes: initialAttributes, initialInfo: initialInfo, initialValue: initialValue, accumulatorObjectType: accumulatorObjectType, flags: flags);

            return new E039AccumulatorSourceHelper(accumObs, updateHoldoffPeriod: updateHoldoffPeriod, e039LogConfigSelect: e039LogConfigSelect);
        }

        /// <summary>
        /// Construction helper method - This method combines accumulator construction (when needed)
        /// The behavior of the ifNeeded object creation is determined by the GenerateCreateAccumulatorUpdateItem method which makes use of the GenerateAccumulatorAddItemAttributes(<paramref name="initialAttributes"/>, <paramref name="initialInfo"/>, <paramref name="initialValue"/>) method.
        /// </summary>
        public static string CreateAccumulatorAndObserver(this IE039TableUpdater tableUpdater, string accumulatorName, out E039AccumulatorObserver accumObs, INamedValueSet initialAttributes = null, E039AccumulatorInfo? initialInfo = null, ValueContainer initialValue = default(ValueContainer), string accumulatorObjectType = Constants.DefaultAccumulatorObjectType, E039ObjectFlags flags = E039ObjectFlags.None)
        {
            var accumulatorID = tableUpdater.CreateAccumulatorID(accumulatorName, accumulatorObjectType: accumulatorObjectType);

            var addItem = GenerateCreateAccumulatorUpdateItem(accumulatorID, initialAttributes: initialAttributes, initialInfo: initialInfo, initialValue: initialValue, flags: flags);

            string ec = tableUpdater.Update(addItem).Run();

            accumObs = new E039AccumulatorObserver(addItem.AddedObjectPublisher);

            return ec;
        }

        /// <summary>
        /// Generates and returns and E039 AddObject UpdateItem that can be used to create and initialize an E039 Accumulator object using the given <paramref name="accumID"/> if needed.
        /// The attributes for this creation are generated using GenerateAccumulatorAddItemAttributes(<paramref name="initialAttributes"/>, <paramref name="initialInfo"/>, <paramref name="initialValue"/>) which is responsible for combining these attribute sources.
        /// The resulting AddObject item uses NamedValueMergeBehavior.AddNewItems and ifNeeded: true.  When the desired accumulator does not already exist the accumulators initial attributes are defined directly from this set.  
        /// When the accumulator already exists then these attributes will be merged with its existing attributes using this merge behavior so only attrribute values that are specified here and are not already present (with this or another value) in the accumulator object will be added to the object.
        /// Any exisiting accumulator attribute value will not be changed by this item.
        /// </summary>
        public static E039UpdateItem.AddObject GenerateCreateAccumulatorUpdateItem(this E039ObjectID accumID, INamedValueSet initialAttributes = null, E039AccumulatorInfo? initialInfo = null, ValueContainer initialValue = default(ValueContainer), E039ObjectFlags flags = E039ObjectFlags.None)
        {
            var addItemAttributes = GenerateAccumulatorAddItemAttributes(initialAttributes, initialInfo, initialValue);

            return new E039UpdateItem.AddObject(accumID, attributes: addItemAttributes, flags: flags | Settings.DefaultAddObjectFlags, ifNeeded: true, mergeBehavior: NamedValueMergeBehavior.AddNewItems);
        }

        /// <summary>
        /// This method is used to generate a set of initial attributes for Accumulator creation.  
        /// This set of attributes is generated from the following sources, in priority order:
        /// <para/><paramref name="initialValue"/> when IsNeitherNullNorEmpty this field is used to define the value of the Value attribute.
        /// <para/>When the <paramref name="initialInfo"/> is non-null its UpdateAttributes call is used to contribute initial attribute values for its non-null attribute value properties.  The info.Value will only be used if it is not empty and the <paramref name="initialValue"/> IsNullOrEmpty.
        /// <para/>When a non-empty <paramref name="initialAttributes"/> is provided these attributes will be used to define the initial attribute values that are not otherwise specified above.  This can be used to define additional attribute values that are not otherwise know to this accumulator system.
        /// </summary>
        public static INamedValueSet GenerateAccumulatorAddItemAttributes(INamedValueSet initialAttributes, E039AccumulatorInfo? initialInfo, ValueContainer initialValue)
        {
            var addItemAttributes = initialAttributes.MapNullToEmpty();

            if (initialInfo != null)
                addItemAttributes = (initialInfo ?? default(E039AccumulatorInfo)).UpdateAttributeValues(addItemAttributes.ConvertToWritable());

            if (initialValue.IsNeitherNullNorEmpty())
                addItemAttributes = addItemAttributes.ConvertToWritable().SetValue(Constants.ValueAttributeName, initialValue);

            return addItemAttributes;
        }

        #endregion

        #region Accumulator Increment EMs (Increment variants, GenerateAccumulatorIncrementUpdateItem)

        /// <summary>
        /// E039 Accumulator ID extension method: generates and runs an attribute Update Item to increment the selected (or default) attribute with the given <paramref name="incrementValue"/>.
        /// </summary>
        public static string Increment(this E039ObjectID accumulatorID, ValueContainer incrementValue, bool enableUpcasts = true, bool update = true, string logConfigSelect = null)
        {
            var tableMgr = accumulatorID.TableObserver as IE039TableUpdater;
            var icf = tableMgr.Update(accumulatorID.GenerateAccumulatorIncrementUpdateItem(incrementValue, enableUpcasts: true), logConfigSelect: logConfigSelect ?? Settings.DefaultIncrementLogConfigSelect);            
            var ec = icf.Run();
            return ec;
        }

        /// <summary>
        /// E039 Accumulator observer extension method: generates and runs an attribute Update Item to increment the selected (or default) attribute with the given <paramref name="incrementValue"/>.
        /// </summary>
        public static string Increment(this E039AccumulatorObserver accumObs, ValueContainer incrementValue, bool enableUpcasts = true, bool update = true, string logConfigSelect = null)
        {
            string ec = accumObs.ID.Increment(incrementValue, enableUpcasts: enableUpcasts, logConfigSelect: logConfigSelect);

            if (update)
                accumObs.Update();

            return ec;
        }

        /// <summary>
        /// Generates and returns en E039UpdateItem that is used to increment the value of the given <paramref name="accumID"/>'s accumulator by the given <paramref name="incrementValue"/>
        /// </summary>
        public static E039UpdateItem GenerateAccumulatorIncrementUpdateItem(this E039ObjectID accumID, ValueContainer incrementValue, bool enableUpcasts = true)
        {
            var mergeBehavior = NamedValueMergeBehavior.AddAndUpdate | NamedValueMergeBehavior.Sum | (enableUpcasts ? NamedValueMergeBehavior.EnableUpcast : NamedValueMergeBehavior.None);

            return new E039UpdateItem.SetAttributes(accumID, new NamedValueSet() { { Constants.ValueAttributeName, incrementValue } }.MakeReadOnly(), mergeBehavior: mergeBehavior);
        }

        #endregion

        #region ClearValue, SetValues EM variants (ClearValue, SetValues, GenerateE039AccumulatorSetValuesUpdateItem)

        /// <summary>
        /// Calls <paramref name="accumObs"/>.SetValues passing in the most recently observed <paramref name="accumObs"/>.Info.Value with its value set to zero.  If the observed Value is not numeric then this method sets the accumulator's value to <paramref name="fallbackZeroValue"/> which default to default(ValueContainer) (aka Empty).
        /// </summary>
        public static string Clear(this E039AccumulatorObserver accumObs, bool update = true, string logConfigSelect = null, ValueContainer fallbackZeroValue = default(ValueContainer))
        {
            return accumObs.SetValues(accumObs.Info.Value.SetZero(fallbackValue: fallbackZeroValue), update: update, logConfigSelect: logConfigSelect);
        }

        /// <summary>
        /// Generates and runs an E039 Accumulator SetValues item for the given <paramref name="accumObs"/> to set each of the of the given non-null values.
        /// </summary>
        public static string SetValues(this E039AccumulatorObserver accumObs, ValueContainer? value = null, VCRange? range1 = null, VCRange? range2 = null, bool update = true, string logConfigSelect = null)
        {
            string ec = accumObs.ID.E039AccumulatorSetValues(value: value, range1: range1, range2: range2, logConfigSelect: logConfigSelect);

            if (update)
                accumObs.Update();

            return ec;
        }

        /// <summary>
        /// Generates and runs an E039 Accumulator SetValues item for the given <paramref name="accumID"/> to set each of the of the given non-null values.
        /// </summary>
        public static string E039AccumulatorSetValues(this E039ObjectID accumID, ValueContainer? value = null, VCRange? range1 = null, VCRange? range2 = null, string logConfigSelect = null)
        {
            var tableMgr = accumID.TableObserver as IE039TableUpdater;

            var icf = tableMgr.Update(accumID.GenerateE039AccumulatorSetValuesUpdateItem(value: value, range1: range1, range2: range2), logConfigSelect);

            string ec = icf.Run();

            return ec;
        }

        /// <summary>
        /// Generates and returns an E039UpdateItem that is used to optionally set the given <paramref name="accumID"/>'s accumulator Value, Range1 and Range2 to the correspondingly given non-null parameters.
        /// </summary>
        public static E039UpdateItem GenerateE039AccumulatorSetValuesUpdateItem(this E039ObjectID accumID, ValueContainer? value = null, VCRange? range1 = null, VCRange? range2 = null)
        {
            var attributes = new NamedValueSet().ConditionalSetValue(Constants.ValueAttributeName, value != null, value ?? ValueContainer.Empty);

            if (range1 != null)
            {
                var range = range1 ?? default(VCRange);
                attributes.SetValue(Constants.Range1LowAttributeName, range.Low);
                attributes.SetValue(Constants.Range1HighAttributeName, range.High);
            }

            if (range2 != null)
            {
                var range = range2 ?? default(VCRange);
                attributes.SetValue(Constants.Range2LowAttributeName, range.Low);
                attributes.SetValue(Constants.Range2HighAttributeName, range.High);
            }

            return new E039UpdateItem.SetAttributes(accumID, attributes, NamedValueMergeBehavior.AddAndUpdate);
        }

        #endregion
    }

    #endregion
}