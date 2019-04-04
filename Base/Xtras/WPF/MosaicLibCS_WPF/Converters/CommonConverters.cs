//-------------------------------------------------------------------
/*! @file CommonConverters.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Globalization;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Part;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.WPF.Converters
{
    #region OneWayValueConverterBase, OneWayMultiValueConverterBase

    /// <summary>
    /// Base class for One Way Value Converter classes.  ConvertBack return Binding.DoNothing by default.
    /// </summary>
    public abstract class OneWayValueConverterBase : IValueConverter
    {
        public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);

        public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Base class for One Way Multi Value Converter classes.  ConvertBack throws a NotImplementedException.
    /// </summary>
    public abstract class OneWayMultiValueConverterBase : IMultiValueConverter
    {
        public abstract object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion

    #region ObjectToStringConverter, VCToStringSMLConverter, SetToStringConverter

    /// <summary>
    /// Supports bindable, one way, conversion of an object to a string by calling SafeToString() on it.
    /// <para/>The ConvertBack method alsways returns Binding.DoNothing
    /// </summary>
    public class ObjectToStringConverter : OneWayValueConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.SafeToString();
        }
    }

    /// <summary>
    /// Supports bindable, one way, conversion of an object to a string by creating a value container for the given object and then calling ToStringSML on it.
    /// <para/>The ConvertBack method alsways returns Binding.DoNothing
    /// </summary>
    public class VCToStringSMLConverter : OneWayValueConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
			return ValueContainer.CreateFromObject(value).ToStringSML();
        }
    }

    /// <summary>
    /// Supports bindable, one way, conversion of any IEnumerable object to a string using String.Join(", ", set.Select(o.ToString()))
    /// </summary>
    public class SetToStringConverter : OneWayValueConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            IEnumerable set = value as IEnumerable;
            string delimiter = (parameter as string) ?? ", ";

            return String.Join(delimiter, set.SafeToSet().Select(o => o.SafeToString()));
        }
    }

    #endregion

    #region AppendParameterConverter(s)

    /// <summary>
    /// This value converter appends the given string ConverterParameter to the given string value, inserting the given delimiter between them (as needed when both are non-empty)
    /// </summary>
    public class AppendParameterWithDelimiterConverter : OneWayValueConverterBase
    {
        public AppendParameterWithDelimiterConverter(string delimiter)
        {
            Delimiter = delimiter.MapNullToEmpty();
        }

        public string Delimiter { get; private set; }

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var valueStr = value.SafeToString();
            var parameterStr = parameter.SafeToString();

            if (valueStr.IsNullOrEmpty())
                return parameterStr;
            else if (parameterStr.IsNullOrEmpty())
                return valueStr;
            else
                return string.Concat(valueStr, Delimiter, parameterStr);
        }
    }

    /// <summary>
    /// Variant of AppendParameterWithDelimiterConverter that inserts spaces between the value and the parameter when both are non-empty
    /// </summary>
    public class AppendParameterWithSpaceDelimiterConverter : AppendParameterWithDelimiterConverter
    {
        /// <summary></summary>
        public AppendParameterWithSpaceDelimiterConverter() : base(" ") {}
    }

    #endregion

    #region IndexIntoSplitStringConverter, IndexIntoCommaDelimitedStringConverter

    /// <summary>
    /// This value converter accepts an integer value and uses it to select, and return, the indexed string as extracted from the parameter value split using the construction provided delimiter character.
    /// </summary>
    public class IndexIntoSplitStringConverter : OneWayValueConverterBase
    {
        /// <summary>Constructor - requires caller to provide the delimiter to use.</summary>
        public IndexIntoSplitStringConverter(char delimiter, bool clipInt = true, MapNullOrEmptyStringTo mapNullOrEmptyStringTo = MapNullOrEmptyStringTo.Binding_DoNothing)
        {
            Delimiter = delimiter;
            ClipInt = clipInt;
        }

        public char Delimiter { get; private set; }
        public bool ClipInt { get; private set; }
        public MapNullOrEmptyStringTo MapNullOrEmptyStringTo { get; private set; }

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int indexFromValue = ValueContainer.CreateFromObject(value).GetValue<int>(rethrow: false);

            string [] tokenSetArray = parameter.SafeToString().Split(Delimiter);

            string result = (ClipInt && !tokenSetArray.IsNullOrEmpty()) ? tokenSetArray.SafeAccess(indexFromValue.Clip(0, tokenSetArray.Length - 1))
                                                                        : tokenSetArray.SafeAccess(indexFromValue);

            if (result.IsNeitherNullNorEmpty())
                return result;

            switch (MapNullOrEmptyStringTo)
            {
                default:
                case Converters.MapNullOrEmptyStringTo.NoChange: return result;
                case Converters.MapNullOrEmptyStringTo.Null: return null;
                case Converters.MapNullOrEmptyStringTo.Empty: return "";
                case Converters.MapNullOrEmptyStringTo.Binding_DoNothing: return Binding.DoNothing;
            }
        }
    }

    /// <summary>
    /// This enumeration is used to determine what the IndexInfoSplitStringConverter (and derived converters) should return if the selected item is null or string.Empty.
    /// <para/>Null (0), Empty, Binding_DoNothing
    /// </summary>
    public enum MapNullOrEmptyStringTo
    {
        /// <summary>Returns the indexed value without change (typically empty or null depending on condition)</summary>
        NoChange = 0,

        /// <summary>Returns null</summary>
        Null,

        /// <summary>Returns String.Empty</summary>
        Empty,

        /// <summary>Returns Binding.DoNothing</summary>
        Binding_DoNothing,
    }

    /// <summary>
    /// This value converter accepts an integer value and uses it to select, and return, the indexed string as extracted from the parameter value split using the comma delimeter character (,)
    /// </summary>
    public class IndexIntoCommaDelimitedStringConverter : IndexIntoSplitStringConverter
    {
        /// <summary>Constructor</summary>
        public IndexIntoCommaDelimitedStringConverter() : base(',') {}
    }

    /// <summary>
    /// This value converter accepts an integer value and uses it to select, and return, the indexed string as extracted from the parameter value split using the pipe delimeter character (|)
    /// </summary>
    public class IndexIntoPipeDelimitedStringConverter : IndexIntoSplitStringConverter
    {
        /// <summary>Constructor</summary>
        public IndexIntoPipeDelimitedStringConverter() : base('|') { }
    }

    /// <summary>
    /// This value converter accepts an integer value and uses it to select, and return, the indexed string as extracted from the parameter value split using the colon delimeter character (:)
    /// </summary>
    public class IndexIntoColonDelimitedStringConverter : IndexIntoSplitStringConverter
    {
        /// <summary>Constructor</summary>
        public IndexIntoColonDelimitedStringConverter() : base(':') { }
    }

    /// <summary>
    /// This value converter accepts an integer value and uses it to select, and return, the indexed string as extracted from the parameter value split using the semi-colon delimeter character (;)
    /// </summary>
    public class IndexIntoSemiColonDelimitedStringConverter : IndexIntoSplitStringConverter
    {
        /// <summary>Constructor</summary>
        public IndexIntoSemiColonDelimitedStringConverter() : base(';') { }
    }

    #endregion

    #region NamedValueConverter and NamedValueSetConverter

    /// <summary>
    /// This value converter is passed a string parameter (aka the item name) and returns a NamedValue contains the parameter as a string name associated with the given value.
    /// </summary>
    public class NamedValueConverter : OneWayValueConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new NamedValue(parameter.SafeToString(), ValueContainer.CreateFromObject(value), asReadOnly: false);
        }
    }

    /// <summary>
    /// This multi value converter is passed a set of NamedValues (see NamedValueConverter) or a set of items and generates a NamedValueSet composed of the given set of NamedValues.
    /// Any item that is not an INamedValue will be replaced with a new readonly NamedValue that is composed of the item's index as the name of the NamedValue and with the item as the value of the NamedValue.
    /// </summary>
    public class NamedValueSetConverter : OneWayMultiValueConverterBase
    {
        public override object Convert(object [] values, Type targetType, object parameter, CultureInfo culture)
        {
            var nvSet = values.MapNullToEmpty().Select((item, itemIndex) =>
                {
                    var nv = item as INamedValue;
                    if (nv != null)
                        return nv;

                    return new NamedValue(itemIndex.ToString(), ValueContainer.CreateFromObject(item), asReadOnly: false);
                });

            return new NamedValueSet(nvSet, asReadOnly: true);
        }
    }

    #endregion

    #region PartBaseStateDenyReasonSetConverter, PartBaseStateDenyReasonSetConverterBehavior (et. al.)

    /// <summary>
    /// Supports bindable, one way, conversion of IBaseState objects into an array of strings (deny reason set) based on a specific set of conditions
    /// </summary>
    public class PartBaseStateDenyReasonSetConverter : OneWayValueConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var behavior = (parameter != null) ? ((parameter is PartBaseStateDenyReasonSetConverterBehavior) ? (PartBaseStateDenyReasonSetConverterBehavior)parameter : ValueContainer.CreateFromObject(parameter).GetValue<PartBaseStateDenyReasonSetConverterBehavior>(rethrow: false)) : default(PartBaseStateDenyReasonSetConverterBehavior);

            IBaseState baseState = (value as IBaseState).MapDefaultTo(BaseState.None);
            UseState useState = baseState.UseState;
            ConnState connState = baseState.ConnState;
            bool useStateIsBusy = (useState == UseState.OnlineBusy || useState == UseState.AttemptOnline);
            bool connStateIsBusy = (connState == ConnState.Connecting);
            bool connStateIsApplicable = (connState != ConnState.NotApplicable && connState != ConnState.Undefined);

            BaseState.ToStringSelect toStringSelect = BaseState.ToStringSelect.UseStateNoPrefix | BaseState.ToStringSelect.ConnState | ((useState == UseState.OnlineBusy) ? BaseState.ToStringSelect.ActionName : BaseState.ToStringSelect.Reason);

            switch (behavior)
            {
                case PartBaseStateDenyReasonSetConverterBehavior.IsFullyOnlineAndIdle:
                    if (useStateIsBusy)
                        return "Part '{0}' is not idle: {1}".CheckedFormat(baseState.PartID, baseState.ToString(toStringSelect));
                    else if (!useState.IsOnline(acceptAttemptOnline: false, acceptUninitialized: false, acceptOnlineFailure: false, acceptAttemptOnlineFailed: false))
                        return "Part '{0}' is not online: {1}".CheckedFormat(baseState.PartID, baseState.ToString(toStringSelect));
                    else if (!connState.IsConnected() && connStateIsApplicable)
                        return "Part '{0}' is not connected: {1}".CheckedFormat(baseState.PartID, baseState.ToString(toStringSelect));
                    break;

                case PartBaseStateDenyReasonSetConverterBehavior.IsOnlineAndIdle:
                    if (useStateIsBusy)
                        return "Part '{0}' is not idle: {1}".CheckedFormat(baseState.PartID, baseState.ToString(toStringSelect));
                    else if (!useState.IsOnline(acceptAttemptOnline: false, acceptUninitialized: true, acceptOnlineFailure: true, acceptAttemptOnlineFailed: false))
                        return "Part '{0}' is not online: {1}".CheckedFormat(baseState.PartID, baseState.ToString(toStringSelect));
                    else if (!connState.IsConnected() && connStateIsApplicable)
                        return "Part '{0}' is not connected: {1}".CheckedFormat(baseState.PartID, baseState.ToString(toStringSelect));
                    break;

                case PartBaseStateDenyReasonSetConverterBehavior.IsOnline:
                    if (!useState.IsOnline(acceptAttemptOnline: true, acceptUninitialized: true, acceptOnlineFailure: true, acceptAttemptOnlineFailed: false))
                        return "Part '{0}' is not online: {1}".CheckedFormat(baseState.PartID, baseState.ToString(toStringSelect));
                    break;

                case PartBaseStateDenyReasonSetConverterBehavior.IsNotBusy:
                    if (useStateIsBusy)
                        return "Part '{0}' is not idle: {1}".CheckedFormat(baseState.PartID, baseState.ToString(BaseState.ToStringSelect.AllForPart));
                    break;

                default:
                    return "Invalid requested conversion beahvior [{0}]".CheckedFormat(behavior);
            }

            return EmptyArrayFactory<string>.Instance;
        }
    }

    /// <summary>
    /// PartBaseStateDenyReasonSetConverter variant that makes use of the PartBaseStateDenyReasonSetConverterBehavior.IsFullyOnlineAndIdle
    /// </summary>
    public class PartBaseStateIsFullyOnlineAndIdleDenyReasonSetConverter : PartBaseStateDenyReasonSetConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return base.Convert(value, targetType, PartBaseStateDenyReasonSetConverterBehavior.IsFullyOnlineAndIdle, culture);
        }
    }

    /// <summary>
    /// PartBaseStateDenyReasonSetConverter variant that makes use of the PartBaseStateDenyReasonSetConverterBehavior.IsOnlineAndIdle
    /// </summary>
    public class PartBaseStateIsOnlineAndIdleDenyReasonSetConverter : PartBaseStateDenyReasonSetConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return base.Convert(value, targetType, PartBaseStateDenyReasonSetConverterBehavior.IsOnlineAndIdle, culture);
        }
    }

    /// <summary>
    /// PartBaseStateDenyReasonSetConverter variant that makes use of the PartBaseStateDenyReasonSetConverterBehavior.IsOnline
    /// </summary>
    public class PartBaseStateIsOnlineDenyReasonSetConverter : PartBaseStateDenyReasonSetConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return base.Convert(value, targetType, PartBaseStateDenyReasonSetConverterBehavior.IsOnline, culture);
        }
    }

    /// <summary>
    /// PartBaseStateDenyReasonSetConverter variant that makes use of the PartBaseStateDenyReasonSetConverterBehavior.IsNotBusy
    /// </summary>
    public class PartBaseStateIsNotBusyDenyReasonSetConverter : PartBaseStateDenyReasonSetConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return base.Convert(value, targetType, PartBaseStateDenyReasonSetConverterBehavior.IsNotBusy, culture);
        }
    }

    /// <summary>
    /// Part BaseState deny reason conversion behavior selection.
    /// <para/>IsFullyOnlineAndIdle (0), IsOnlineAndIdle, IsOnline
    /// </summary>
    public enum PartBaseStateDenyReasonSetConverterBehavior : int
    {
        /// <summary>The part's UseState is Online (but not OnlineBusy)</summary>
        IsFullyOnlineAndIdle = 0,

        /// <summary>The part's UseStaet is Online, OnlineUninitialized, or OnlineFailure</summary>
        IsOnlineAndIdle,

        /// <summary>The part's UseState is Online, OnlineBusy, AttemptOnline, OnlineUnititialized, or OnlineFailure</summary>
        IsOnline,

        /// <summary>The part's UseState is neither OnlineBusy nor AttemptOnline</summary>
        IsNotBusy,
    }

    #endregion

    #region FlattenSetConverter, FlattenAndSquishSetConverter, FirstNonEmptyItemOrSetConverter, FlattenSetBehavior and related ExtensionMethods

    /// <summary>
    /// Supports bindable, one way, flatten a set of sets of things into a single set of things.  
    /// The Convert parameter may be passed as a FlattenSetBehavior to control the desired set flattening behavior (either Concatenate or FirstNonEmptyItemOrSet)
    /// This is done by expanding each of the top level IEnumerable objects in the given values array so that the individual elements in each such sub-set are directly included in the resulting set.
    /// This converter supports flattening of mixes of top level objects (non-IEnumerable ones) and IEnumerable ones (which will be flattened into the output set).
    /// </summary>
    public class FlattenSetConverter : OneWayMultiValueConverterBase
    {
        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var flattenSetBehavior = (parameter != null) ? ((parameter is FlattenSetBehavior) ? (FlattenSetBehavior) parameter : ValueContainer.CreateFromObject(parameter).GetValue<FlattenSetBehavior>(rethrow: false)) : default(FlattenSetBehavior);

            return values.FlattenSet(flattenSetBehavior);
        }
    }

    /// <summary>
    /// Supports bindable, one way, converter that "flattens" the given set of items/sets into a single set of items using the FlattenSetBehvior.FlattenAndSquish behavior.
    /// </summary>
    public class FlattenAndSquishSetConverter : FlattenSetConverter
    {
        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return base.Convert(values, targetType, FlattenSetBehavior.FlattenAndSquish, culture);
        }
    }

    /// <summary>
    /// Supports bindable, one way, converter that "flattens" the given set of items/sets into a single set of items using the FlattenSetBehvior.FirstNonEmptyItemOrSet behavior.
    /// </summary>
    public class FirstNonEmptyItemOrSetConverter : FlattenSetConverter
    {
        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return base.Convert(values, targetType, FlattenSetBehavior.FirstNonEmptyItemOrSet, culture);
        }
    }

    /// <summary>
    /// This enumeration defines the set of behaviors that the FlattenSet EM can be selected to use.
    /// <para/>FlattenAndSquish (0), FirstNonEmptyItemOrSet
    /// </summary>
    public enum FlattenSetBehavior : int
    {
        /// <summary>produces an output set where each element in the input set that is an IEnumerable item is replaced in the output set by the item's enumerated contents.</summary>
        FlattenAndSquish = 0,

        /// <summary>
        /// Produces an output set that contains the non-null and non-empty first element of the input set "flattened".  
        /// The input set is flattend and squished with empty or null items removed.  Then the first such non-empty item is returned, either as its set contents if it is a set, or as a set containing the first item if it was a non-empty string or a non-null object.
        /// </summary>
        FirstNonEmptyItemOrSet,
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        private static readonly object[] emptyObjectArray = EmptyArrayFactory<object>.Instance;
        private static readonly string[] emptyStringArray = EmptyArrayFactory<string>.Instance;

        /// <summary>
        /// Returns the given <paramref name="setIn"/> flattened using the given <paramref name="flattenSetBehavior"/>.
        /// For Concatenate behavior the output set is the set of non-null and non-empty items from the input set where any IEnumerable item is replaced with the sequence of its content items.
        /// For FirstNonEmptyItemOrSet
        /// </summary>
        public static IEnumerable<object> FlattenSet(this IEnumerable<object> setIn, FlattenSetBehavior flattenSetBehavior)
        {
            switch (flattenSetBehavior)
            {
                case FlattenSetBehavior.FlattenAndSquish:
                    return setIn.FlattenAndSquish(mapEmptyResultToNull: true).WhereIsNotDefault().ToArray();

                case FlattenSetBehavior.FirstNonEmptyItemOrSet:
                    return setIn.Select(item => item.FlattenAndSquish(mapEmptyResultToNull: true)).WhereIsNotDefault().FirstOrDefault().MapNullToEmpty();

                default:
                    return emptyObjectArray;
            }
        }

        /// <summary>
        /// Returns an output set that is the <paramref name="objIn"/> input item flattened and squished.  
        /// If the input item is a set then this method return the set resulting from applying this FlattenAndSquish method to each sub-set item from the given set.
        /// If the input item is a string then this method yeilds an set containing the string if it is non-empty, or the empty/null set otherwise (based on <paramref name="mapEmptyResultToNull"/>).
        /// If the input item is none of the obove then this method yeilds a set containing the input item if it is non-null.
        /// In all other cases this method yeilds the empty set, or null if <paramref name="mapEmptyResultToNull"/> is true.
        /// </summary>
        public static IEnumerable<object> FlattenAndSquish(this object objIn, bool mapEmptyResultToNull = false)
        {
            var nullOrEmptyObjectResult = (mapEmptyResultToNull ? null : emptyObjectArray);
            var nullOrEmptyStringResult = (mapEmptyResultToNull ? null : emptyStringArray);

            string objInAsStr = objIn as string;

            if (objInAsStr != null)
                return objInAsStr.IsNeitherNullNorEmpty() ? new object[] { objInAsStr } : nullOrEmptyStringResult;

            IEnumerable<string> objInAsStrSet = objIn as IEnumerable<string>;
            if (objInAsStrSet != null)
                return objInAsStrSet.Where(str => str.IsNeitherNullNorEmpty()).ToArray().MapEmptyTo(nullOrEmptyStringResult);

            IEnumerable objInAsSet = objIn as IEnumerable;
            if (objInAsSet != null)
                return objInAsSet.SafeToSet().SelectMany(item => item.FlattenAndSquish(mapEmptyResultToNull).WhereIsNotDefault()).ToArray().MapEmptyTo(nullOrEmptyObjectResult);

            if (objIn != null)
                return new[] { objIn };

            return nullOrEmptyObjectResult;
        }
    }

    #endregion
}
