//-------------------------------------------------------------------
/*! @file InterconnectSupport.cs
 *  @brief This file provides classes that support use of Interconnect related services under WPF
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved.
 * Copyright (c) 2015 Mosaic Systems Inc., All rights reserved.
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using MosaicLib;
using MosaicLib.Utils;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Interconnect.Values;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Threading;
using System.Windows;

namespace MosaicLib.WPF.Interconnect
{
    #region WPFValueAccessor and DataContext helper

    /// <summary>
    /// This class is used by with the WPFValueInterconnectAdapter to provide a simple connection between an IValueInterconnect and WPF using Bindable objects.
    /// WPFValueInterconnectAdapter provides a means to find a WPFValueAccessor for a given named value and handles servicing of the set of WPFValueAccessors
    /// that WPF has found from the adapter (called the active set).
    /// This object provides a set of dependency properties: ValueAsObject, ValueAsDouble and ValueAsBoolean, which may be used directly in the screen code.
    /// This object provides the glue logic that is used to replicate changes to the these DP's into their corresponding ValueAccessors and to replciate changes to the ValueAccessor
    /// back into the corresponding dependency properties when the ValueAccessors have been updated with new values.
    /// <para/>This object is only constructed by WPFValueInterconnectAdapter instances.
    /// </summary>
    public class WPFValueAccessor : DependencyObject
    {
        /// <summary>internal Contructor.  Requires the corresponding IValueAccessor to which it is connected</summary>
        internal WPFValueAccessor(IValueAccessor iva)
        {
            ValueAccessor = iva;
        }

        /// <summary>Retains the IValueAccessor instance to which this object is connected.</summary>
        internal IValueAccessor ValueAccessor { get; private set; }

        internal UInt32 LastServicedValueSeqNum { get; set; }

        /// <summary>This property returns true if the ValueAccessor.IsUpdatedNeeded or (LastServicedValueSeqNum != ValueAccessor.ValueSeqNum).</summary>
        /// <remarks>This property convers the case where a WVA is added after the matching IVA has been given its initial value (ie LastServicedValueSeqNum is zero and ValueAccessor.ValueSeqNum is not).</remarks>
        internal bool IsUpdateNeeded { get { return (ValueAccessor.IsUpdateNeeded || (LastServicedValueSeqNum != ValueAccessor.ValueSeqNum)); } }

        /// <summary>
        /// Callback from WPFValueInteronnectAdapater after the connected IValueAccessor has been updated to trigger the WVA to distribute the value to the corresponding dependency properties.
        /// </summary>
        /// <remarks>
        /// The DependencyObject.GetValue method is viewed as being relatively expensive.
        /// </remarks>
        internal void NotifyValueHasBeenUpdated()
        {
            inNotifyValueHasBeenUpdated = true;

            // update local copy of the ValueAccessor's ValueSeqNum
            LastServicedValueSeqNum = ValueAccessor.ValueSeqNum;

            // extract new values from the IVA's ValueContainer
            ValueContainer ivaVC = ValueAccessor.ValueContainer;
            object valueAsObject = ivaVC.ValueAsObject;
            string valueAsString = valueAsObject as string;

            double? valueAsDouble = null;
            bool? valueAsBoolean = null;
            Int32? valueAsInt32 = null;

            // decode the contained value types to the corresponding closest nullable type
            // floats -> valueAsDouble
            // bool -> valueAsBoolean
            // integers -> valueAsInt32 (if value is in supported range).
            // bit integers also go to valueAsDouble using System.Convert.ToDouble
            switch (ivaVC.cvt)
            {
                case ContainerStorageType.Single: 
                    valueAsDouble = ivaVC.u.f32;
                    break;
                case ContainerStorageType.Double: 
                    valueAsDouble = ivaVC.u.f64; 
                    break;
                case ContainerStorageType.Boolean: 
                    valueAsBoolean = ivaVC.u.b; 
                    break;
                case ContainerStorageType.Byte: 
                    valueAsInt32 = ivaVC.u.i8;
                    break;
                case ContainerStorageType.Int16:
                    valueAsInt32 = ivaVC.u.i16;
                    break;
                case ContainerStorageType.Int32:
                    valueAsInt32 = ivaVC.u.i32;
                    break;
                case ContainerStorageType.Int64:
                    if (ivaVC.u.i64 >= Int32.MinValue && ivaVC.u.i64 <= Int32.MaxValue)
                        valueAsInt32 = unchecked((Int32) ivaVC.u.i64);
                    valueAsDouble = System.Convert.ToDouble(ivaVC.u.i64); 
                    break;
                case ContainerStorageType.SByte:
                    valueAsInt32 = ivaVC.u.u8;
                    break;
                case ContainerStorageType.UInt16:
                    valueAsInt32 = ivaVC.u.u16;
                    break;
                case ContainerStorageType.UInt32:
                    if (ivaVC.u.u32 <= Int32.MaxValue)
                        valueAsInt32 = unchecked((Int32)ivaVC.u.u32);
                    break;
                case ContainerStorageType.UInt64:
                    if (ivaVC.u.u64 <= Int32.MaxValue)
                        valueAsInt32 = unchecked((Int32)ivaVC.u.u64);
                    valueAsDouble = System.Convert.ToDouble(ivaVC.u.u64);
                    break;
            }

            // perform an implicit cast of any int32 or double value to a boolean where the boolean is true if the number is not equal to zero
            if (valueAsBoolean == null)
            {
                if (valueAsInt32 != null)
                    valueAsBoolean = (valueAsInt32.GetValueOrDefault() != 0);
                else if (valueAsDouble != null)
                    valueAsBoolean = (valueAsDouble.GetValueOrDefault() != 0.0);
            }

            // perform an implict cast of any int32 value to a double if the above code did not already do this.
            if (valueAsDouble == null && valueAsInt32 != null)
                valueAsDouble = unchecked((double)valueAsInt32.GetValueOrDefault());

            // determin which versions of the ValueContainer's contents are different.
            bool setContainerDP = !lastVC.IsEqualTo(ivaVC);
            bool setObjectDP = !Object.ReferenceEquals(lastValueAsObject, valueAsObject);
            bool setDoubleDP = (lastValueAsDouble != valueAsDouble);
            bool setBooleanDP = (lastValueAsBoolean != valueAsBoolean);
            bool setInt32DP = (lastValueAsInt32 != valueAsInt32);
            bool setStringDP = (lastValueAsString != valueAsString);

            // set the changed dependency properties inline.  This does not create a risk of recursion because this callstack allways originates with the WVIA's Service method.
            // Any WVA DP update loop will terminate in the OnPropertyChanged method below which just sets the corresonding IVA to the final value.

            if (setContainerDP)
                SetValue(vcDP, ivaVC);

            if (setObjectDP)
                SetValue(valueAsObjectDP, valueAsObject);

            if (setDoubleDP)
                SetValue(valueAsDoubleDP, valueAsDouble);

            if (setBooleanDP)
                SetValue(valueAsBooleanDP, valueAsBoolean);

            if (setInt32DP)
                SetValue(valueAsInt32DP, valueAsInt32);

            if (setStringDP)
                SetValue(valueAsStringDP, valueAsString);

            inNotifyValueHasBeenUpdated = false;
        }

        private bool inNotifyValueHasBeenUpdated = false;

        private ValueContainer lastVC = ValueContainer.Empty;
        private object lastValueAsObject = null;
        private double? lastValueAsDouble = null;
        private bool? lastValueAsBoolean = null;
        private Int32? lastValueAsInt32 = null;
        private string lastValueAsString = null;

        /// <summary>
        /// Callback from WPF to tell us that one of the dependency properties has been changed.
        /// For the dependency properties that are registered here, this method generates a ValueContainer from the property value 
        /// and then sets the ValueAccessor to contain the new value, if it is different than the IVA's current value.
        /// </summary>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == vcDP || e.Property == valueAsObjectDP || e.Property == valueAsDoubleDP || e.Property == valueAsBooleanDP || e.Property == valueAsInt32DP || e.Property == valueAsStringDP)
            {
                ValueContainer newValueVC = new ValueContainer().SetFromObject(e.NewValue);

                // we only pass the newValueVC back to the ValueAccessor if the OnPropertyChanged callback is not a direct result of logic in the NotifyValueHasBeenUpdated method

                if (!inNotifyValueHasBeenUpdated)
                {
                    if (!ValueAccessor.ValueContainer.IsEqualTo(newValueVC))
                    {
                        ValueAccessor.Set(newValueVC);
                        lastVC = newValueVC;
                    }
                }

                if (e.Property == vcDP)
                    lastVC = newValueVC;
                else if (e.Property == valueAsObjectDP)
                    lastValueAsObject = e.NewValue;
                else if (e.Property == valueAsDoubleDP)
                    lastValueAsDouble = e.NewValue as double?;
                else if (e.Property == valueAsBooleanDP)
                    lastValueAsBoolean = e.NewValue as bool?;
                else if (e.Property == valueAsInt32DP)
                    lastValueAsInt32 = e.NewValue as Int32?;
                else if (e.Property == valueAsStringDP)
                    lastValueAsString = e.NewValue as String;
            }
            else
            {
                base.OnPropertyChanged(e);
            }
        }

        private static System.Windows.DependencyProperty vcDP = System.Windows.DependencyProperty.Register("VC", typeof(ValueContainer), typeof(WPFValueAccessor));
        private static System.Windows.DependencyProperty valueAsObjectDP = System.Windows.DependencyProperty.Register("ValueAsObject", typeof(System.Object), typeof(WPFValueAccessor));
        private static System.Windows.DependencyProperty valueAsDoubleDP = System.Windows.DependencyProperty.Register("ValueAsDouble", typeof(System.Double?), typeof(WPFValueAccessor));
        private static System.Windows.DependencyProperty valueAsBooleanDP = System.Windows.DependencyProperty.Register("ValueAsBoolean", typeof(System.Boolean?), typeof(WPFValueAccessor));
        private static System.Windows.DependencyProperty valueAsInt32DP = System.Windows.DependencyProperty.Register("ValueAsInt32", typeof(System.Int32?), typeof(WPFValueAccessor));
        private static System.Windows.DependencyProperty valueAsStringDP = System.Windows.DependencyProperty.Register("ValueAsString", typeof(System.String), typeof(WPFValueAccessor));

        public override string ToString()
        {
            return "WVA for:{0}".CheckedFormat(ValueAccessor);
        }
    }

    #endregion

    #region IWPFValueAccessorFactory and IWPFValueAccessorSubTreeFactory

    /// <summary>
    /// This is the interface that various WPFValueAccessor related helper classes support to allow the caller to create/find the WPFValueAccessor for a given name.
    /// <para/>Typically this object is used as a DataContext for a control that then uses Binding statements with ["name"] lookup methods to get the individual WPFValueAccessors
    /// for each given name.
    /// </summary>
    public interface IWPFValueAccessorFactory
    {
        WPFValueAccessor this[string name] { get; }

        IWPFValueAccessorSubTreeFactory SubTreeFactory { get; }
    }

    /// <summary>
    /// This is the interface that the subtree factory factory supports to allow the caller to create a IWPFValueAccessorBindingHelper for a given name.
    /// <para/>Typically this object is used as a DataContext for a control that then uses Binding statements with ["name"] lookup methods to get the individual WPFValueAccessors
    /// for each given name.
    /// </summary>
    public interface IWPFValueAccessorSubTreeFactory
    {
        /// <summary>
        /// Gets/Creates the WPFValueAccessorSubTreeFactory associated with the given name, creating a new one if it does not exist already.
        /// </summary>
        /// <param name="name">The name/key of the WPFValueAccessorSubTreeFactory instance to get.</param>
        /// <returns>The WPFValueAccessorSubTreeFactory assocated with the given name.</returns>
        /// <exception cref="System.ArgumentNullException">name is null.</exception>
        IWPFValueAccessorFactory this[string name] { get; }
    }
    
    #endregion

    #region WPFValueAccessorSubTreeFactoryFactory, WPFValueAccessorSubTreeFactory

    /// <summary>
    /// This class is used to support SubTreeFactory properties on other classes that can be used with WPF Binding Path statements
    /// such as {Binding Path=SubTreeFactory[prefixStringHere]} to create (or re-obtain) a WPFValueAccessorSubTreeFactory for the given "prefixStringHere" sub-tree.
    /// </summary>
    public class WPFValueAccessorSubTreeFactoryFactory : Dictionary<string, WPFValueAccessorSubTreeFactory>, IWPFValueAccessorSubTreeFactory
    {
        /// <summary>
        /// Constructor is given the baseSubTreePrefix from the object that created this factory object and the baseWVIA that is the actual factory for all of the created
        /// WPFValueAccessor objects that get created here.
        /// </summary>
        internal WPFValueAccessorSubTreeFactoryFactory(string baseSubTreePrefix, WPFValueInterconnectAdapter baseWVIA)
        {
            BaseSubTreePrefix = baseSubTreePrefix;
            BaseWVIA = baseWVIA;
        }

        /// <summary>
        /// Thie gives the accumulated base SubTreePrefix from the parent object that created this factory object.  
        /// It will be prefixed onto each of the names given to this object when constructing WPFValueAccessorSubTreeFactory objects so that they will be created with a full prefix
        /// even when this pattern is repeated for multiple layers.
        /// </summary>
        protected string BaseSubTreePrefix { get; set; }

        /// <summary>Gives the base WPFValueInterconnectAdapter that will be used by all of the WPFValueAccessorSubTreeFactory objects that are created by this factory object.</summary>
        protected WPFValueInterconnectAdapter BaseWVIA { get; set; }

        #region Dictionary overrides

        /// <summary>
        /// Local "override" for base Dicationary's this[name] method.
        /// Gets/Creates the WPFValueAccessorSubTreeFactory associated with the given name, creating a new one if it does not exist already.
        /// </summary>
        /// <param name="name">The name/key of the WPFValueAccessorSubTreeFactory instance to get.</param>
        /// <returns>The WPFValueAccessorSubTreeFactory assocated with the given name.</returns>
        /// <exception cref="System.ArgumentNullException">name is null.</exception>
        public new WPFValueAccessorSubTreeFactory this[string name]
        {
            get
            {
                WPFValueAccessorSubTreeFactory subTreeFactory = null;

                if (name != null && !base.ContainsKey(name))
                {
                    subTreeFactory = new WPFValueAccessorSubTreeFactory(BaseSubTreePrefix + name, BaseWVIA);
                    base[name] = subTreeFactory;
                }
                else
                {
                    subTreeFactory = base[name];
                }

                return subTreeFactory;
            }
            set
            {
                throw new System.InvalidOperationException("Item[] setter cannot be used here");
            }
        }

        IWPFValueAccessorFactory IWPFValueAccessorSubTreeFactory.this[string name]
        {
            get { return this[name]; }
        }

        #endregion
    }

    /// <summary>
    /// This class acts as DataContext proxy replacement for the WPFValueInterconnectAdapter so that all WPFValueAccessors that are created by this DataContext object will have the
    /// constructed prefix added to the indexed name in resulting WPF Binding statements.  This is combined with the WPFValueAcessorSubTreeFactoryFactory that is used as a value returned by
    /// the SubTreeFactory property to create these objects with desired subTreePrefix values.  This both this class and the WPFValueInterconnectAdapter on which it is based support this
    /// SubTreeFactory property which allows the sub-tree concept to be repeated in layers.
    /// </summary>
    public class WPFValueAccessorSubTreeFactory : Dictionary<string, WPFValueAccessor>, IWPFValueAccessorFactory
    {
        internal WPFValueAccessorSubTreeFactory(string subTreePrefix, WPFValueInterconnectAdapter baseWVIA) 
        {
            SubTreePrefix = subTreePrefix;
            BaseWVIA = baseWVIA;
        }

        /// <summary>Gives the accumulated SubTreePrefix for this factory that will be prefixed to given names in order to fully specify the name of each WPFValueAccessor that is created here.</summary>
        protected string SubTreePrefix { get; set; }

        /// <summary>Gives the base WPFValueInterconnectAdapter that will be used to create all of the WPFValueAccessor objects that this factory is asked to create.</summary>
        protected WPFValueInterconnectAdapter BaseWVIA { get; set; }

        #region SubTreeFactory property for use in Path statements

        /// <summary>
        /// This property returns a WPFValueAccessorSubTreeFactory Factory object starting at the current SubTreePrefix in the BaseWVIA's namespace. 
        /// This is typically used in the XAML to create DataContext as DC.SubStreeFactory[SubSubTreePrefix]
        /// </summary>
        public WPFValueAccessorSubTreeFactoryFactory SubTreeFactory
        {
            get
            {
                if (subTreeFactory == null)
                    subTreeFactory = new WPFValueAccessorSubTreeFactoryFactory(SubTreePrefix, BaseWVIA);

                return subTreeFactory;
            }
        }

        private WPFValueAccessorSubTreeFactoryFactory subTreeFactory = null;

        IWPFValueAccessorSubTreeFactory IWPFValueAccessorFactory.SubTreeFactory
        {
            get { return this.SubTreeFactory; }
        }

        #endregion

        #region Dictionary overrides

        /// <summary>
        /// Local "override" for base Dicationary's this[name] method.
        /// Gets the WPFValueAccessor associated with the given name, obtaining one from the Base.
        /// </summary>
        /// <param name="name">The name/key of the WPFValueAccessor instance to get.</param>
        /// <returns>The WPFValueAccessor assocated with the given name.</returns>
        /// <exception cref="System.ArgumentNullException">name is null.</exception>
        public new WPFValueAccessor this[string name]
        {
            get
            {
                WPFValueAccessor wva = null;

                if ((name != null) && !base.ContainsKey(name))
                {
                    wva = BaseWVIA[SubTreePrefix + name];
                    base[name] = wva;
                }
                else
                {
                    wva = base[name];
                }

                return wva;
            }
            set
            {
                throw new System.InvalidOperationException("Item[] setter cannot be used here");
            }
        }

        WPFValueAccessor IWPFValueAccessorFactory.this[string name]
        {
            get { return this[name]; }
        }

        #endregion
    }

    #endregion

    #region WPFValueInterconnectAdapter

    /// <summary>
    /// This object provides a self expanding "Dictionary" of named WPFValueAccessor objects that can be setup as an indexed DataContext from which named WPFValueAccessors can be found and bound to.
    /// </summary>
    /// <remarks>
    /// This object is often used with the SubTreeFactory property to get access to a WPFVAlueAdapterSubTreeFactoryFactory that can then be used to create a named
    /// WPFValueAdapterSubTreeFactory.  The resulting WPFValueAdapterSubTreeFactory is then used as a DataContext for a sub-portion of the overall WPF control space and allows it to use
    /// Binding statements that refere to items in the SubTree using the prefix that was given to the WPFVAlueAdapterSubTreeFactoryFactory.
    /// 
    /// In addition we have found that the INotifyCollectionChanged and INotifyPropertyChanged interfaces are not useful for this collection object.  Instead this collection object
    /// simply creates the WPFValueAdapters on the fly (along with their backing IValueAccessors) as Binding statements ask for them.
    /// </remarks>
    public class WPFValueInterconnectAdapter : Dictionary<string, WPFValueAccessor>, IWPFValueAccessorFactory
    {
        /// <summary>Default constructor - defaults to using the Values.Instance IValueInterconnection object</summary>
        public WPFValueInterconnectAdapter()
            : this(Modular.Interconnect.Values.Values.Instance)
        {
        }

        /// <summary>Constructor allows the caller to give the IValuesInterconnection instance to use.</summary>
        public WPFValueInterconnectAdapter(IValuesInterconnection valuesInterconnectionInstanceToUse)
        {
            ivi = valuesInterconnectionInstanceToUse;
        }

        /// <summary>
        /// Service method.  Scans and updates active items which have new values.  Adds new named values to the dictionary as they are found in the IVI's table.
        /// <para/>Supports call chaining.
        /// </summary>
        public WPFValueInterconnectAdapter Service()
        {
            UpdateActiveItems();

            return this;
        }

        #region SubTreeFactory property for use in Path statements

        /// <summary>
        /// This property returns a WPFValueAccessorSubTreeFactory Factory object starting at the root of the current WPFValueInterconnectAdapter namespace  
        /// This is typically used in the XAML to create DataContext as DC.SubStreeFactory[SubTreePrefix]
        /// </summary>
        public WPFValueAccessorSubTreeFactoryFactory SubTreeFactory
        {
            get
            {
                if (subTreeFactory == null)
                    subTreeFactory = new WPFValueAccessorSubTreeFactoryFactory(String.Empty, this);

                return subTreeFactory;
            }
        }

        private WPFValueAccessorSubTreeFactoryFactory subTreeFactory = null;

        IWPFValueAccessorSubTreeFactory IWPFValueAccessorFactory.SubTreeFactory
        {
            get { return this.SubTreeFactory; }
        }

        #endregion

        #region Dictionary overrides

        /// <summary>
        /// Local "override" for base Dicationary's this[name] method.
        /// Gets the WPFValueAccessor associated with the given name, creating a new one if it does not exist already.
        /// </summary>
        /// <param name="name">The name/key of the WPFValueAccessor instance to get.</param>
        /// <returns>The WPFValueAccessor assocated with the given name.</returns>
        /// <exception cref="System.ArgumentNullException">name is null.</exception>
        public new WPFValueAccessor this[string name]
        {
            get
            {
                WPFValueAccessor wva = null;

                if ((name != null) && !base.ContainsKey(name))
                {
                    IValueAccessor iva = ivi.GetValueAccessor(name);
                    wva = new WPFValueAccessor(iva);

                    activeWvaList.Add(wva);
                    rebuildArrays = true;

                    base[name] = wva;
                }
                else
                {
                    wva = base[name];
                }

                return wva;
            }
            set
            {
                throw new System.InvalidOperationException("Item[] setter cannot be used here");
            }
        }

        WPFValueAccessor IWPFValueAccessorFactory.this[string name]
        {
            get { return this[name]; }
        }

        #endregion

        private IValuesInterconnection ivi;

        private List<WPFValueAccessor> activeWvaList = new List<WPFValueAccessor>();
        private bool rebuildArrays = true;
        private IValueAccessor[] activeIvaArray = null;
        private WPFValueAccessor[] wvaUpdateArray = null;
        private IValueAccessor[] ivaUpdateArray = null;

        private void UpdateActiveItems()
        {
            if (rebuildArrays)
            {
                activeIvaArray = activeWvaList.Select((wva) => wva.ValueAccessor).ToArray();
                wvaUpdateArray = new WPFValueAccessor[activeWvaList.Count];
                ivaUpdateArray = new IValueAccessor[activeWvaList.Count];
            }

            int numItemsToCheck = activeIvaArray.Length;
            int numItemsToUpdate = 0;

            for (int checkWvaIdx = 0; checkWvaIdx < numItemsToCheck; checkWvaIdx++)
            {
                WPFValueAccessor wva = activeWvaList[checkWvaIdx];

                if (wva.IsUpdateNeeded || rebuildArrays)
                {
                    wvaUpdateArray[numItemsToUpdate] = wva;
                    ivaUpdateArray[numItemsToUpdate++] = activeIvaArray[checkWvaIdx];
                }
            }

            ivi.Update(ivaUpdateArray, numItemsToUpdate);

            for (int updateWvaIdx = 0; updateWvaIdx < numItemsToUpdate; updateWvaIdx++)
            {
                WPFValueAccessor wva = wvaUpdateArray[updateWvaIdx];
                wva.NotifyValueHasBeenUpdated();
            }

            rebuildArrays = false;
        }
    }


    #endregion
}
