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
    #region WPFValueAccessor and WPFValueInterconnectAdapter

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

        /// <summary>True if the WVA is active, i.e. client code has asked for this WPFValueAccessor instance by name.</summary>
        internal bool IsActive { get; set; }

        /// <summary>Retains the IValueAccessor instance to which this object is connected.</summary>
        internal IValueAccessor ValueAccessor { get; private set; }

        /// <summary>Callback from WPFValueInteronnectAdapater after the connected IValueAccessor has been updated to trigger the WVA to distribute the value to the corresponding dependency properties.</summary>
        internal void NotifyValueHasBeenUpdated()
        {
            // get current DP values.
            object currentValueAsObject = GetValue(valueAsObjectDP);
            double? currentValueAsDouble = (double?)GetValue(valueAsDoubleDP);
            bool? currentValueAsBoolean = (bool?)GetValue(valueAsBooleanDP);

            // extract new values from the IVA's ValueContainer
            ValueContainer valueContainer = ValueAccessor.ValueContainer;
            object valueAsObject = valueContainer.ValueAsObject;
            double? valueAsDouble = valueContainer.ValueAsDouble;
            bool? valueAsBoolean = valueContainer.GetValue<bool?>(ContainerStorageType.Boolean, true, false);

            // determin which versions of the ValueContainer's contents are different.
            bool setObjectDP = !Object.ReferenceEquals(currentValueAsObject, valueAsObject);
            bool setDoubleDP = (currentValueAsDouble != valueAsDouble);
            bool setBooleanDP = (currentValueAsBoolean != valueAsBoolean);

            // set the changed dependency properties inline.  This does not create a risk of recursion because this callstack allways originates with the WVIA's Service method.
            // Any WVA DP update loop will terminate in the OnPropertyChanged method below which just sets the corresonding IVA to the final value.
            if (setObjectDP)
                SetValue(valueAsObjectDP, valueAsObject);

            if (setDoubleDP)
                SetValue(valueAsDoubleDP, valueAsDouble);

            if (setBooleanDP)
                SetValue(valueAsBooleanDP, valueAsBoolean);
        }

        /// <summary>
        /// Callback from WPF to tell us that one of the dependency properties has been changed.
        /// For the dependency properties that are registered here, this method generates a ValueContainer from the property value 
        /// and then sets the ValueAccessor to contain the new value, if it is different than the IVA's current value.
        /// </summary>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == valueAsObjectDP || e.Property == valueAsDoubleDP || e.Property == valueAsBooleanDP)
            {
                ValueContainer newValueVC = new ValueContainer().SetFromObject(e.NewValue);
                if (!ValueAccessor.ValueContainer.IsEqualTo(newValueVC))
                {
                    ValueAccessor.Set(newValueVC);
                }
            }
            else
            {
                base.OnPropertyChanged(e);
            }
        }

        private static System.Windows.DependencyProperty valueAsObjectDP = System.Windows.DependencyProperty.Register("ValueAsObject", typeof(System.Object), typeof(WPFValueAccessor));
        private static System.Windows.DependencyProperty valueAsDoubleDP = System.Windows.DependencyProperty.Register("ValueAsDouble", typeof(System.Double?), typeof(WPFValueAccessor));
        private static System.Windows.DependencyProperty valueAsBooleanDP = System.Windows.DependencyProperty.Register("ValueAsBoolean", typeof(System.Boolean?), typeof(WPFValueAccessor));
    }


    /// <summary>
    /// This object provides a self expanding "Dictionary" of named WPFValueAccessor objects that can be setup as an indexed DataContext from which named WPFValueAccessors can be found and bound to.
    /// </summary>
    public class WPFValueInterconnectAdapter : Dictionary<string, WPFValueAccessor>, INotifyCollectionChanged, INotifyPropertyChanged
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

            ServiceAddedValueNames();

            return this;
        }

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
                if ((name != null) && !base.ContainsKey(name))
                {
                    ivi.GetValueAccessor(name);
                    ServiceAddedValueNames();
                }

                WPFValueAccessor wva = base[name];

                if (!wva.IsActive)
                    ActivateWVA(wva);

                return wva;
            }
            set
            {
                throw new System.InvalidOperationException("Item[] setter cannot be used here");
            }
        }

        #endregion

        /// <summary>Required INotifyCollectionChanged event</summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>Required INotifyPropertyChanged event</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private IValuesInterconnection ivi;

        private List<WPFValueAccessor> wpfValueAccessorList = new List<WPFValueAccessor>();

        private List<WPFValueAccessor> activeWvaList = new List<WPFValueAccessor>();
        private bool rebuildArrays = true;
        private IValueAccessor[] activeIvaArray = null;
        private WPFValueAccessor[] wvaUpdateArray = null;
        private IValueAccessor[] ivaUpdateArray = null;

        private void ActivateWVA(WPFValueAccessor wva)
        {
            activeWvaList.Add(wva);
            rebuildArrays = true;
            wva.IsActive = true;
        }

        private void UpdateActiveItems()
        {
            if (rebuildArrays)
            {
                activeIvaArray = activeWvaList.Select((wva) => wva.ValueAccessor).ToArray();
                wvaUpdateArray = new WPFValueAccessor[activeWvaList.Count];
                ivaUpdateArray = new IValueAccessor[activeWvaList.Count];

                rebuildArrays = false;
            }

            int numItemsToCheck = activeIvaArray.Length;
            int numItemsToUpdate = 0;

            for (int checkIvaIdx = 0; checkIvaIdx < numItemsToCheck; checkIvaIdx++)
            {
                IValueAccessor iva = activeIvaArray[checkIvaIdx];
                if (iva.IsUpdateNeeded)
                {
                    wvaUpdateArray[numItemsToUpdate] = activeWvaList[checkIvaIdx];
                    ivaUpdateArray[numItemsToUpdate++] = iva;
                }
            }

            ivi.Update(ivaUpdateArray, numItemsToUpdate);

            for (int updateWvaIdx = 0; updateWvaIdx < numItemsToUpdate; updateWvaIdx++)
            {
                WPFValueAccessor wva = wvaUpdateArray[updateWvaIdx];
                wva.NotifyValueHasBeenUpdated();
            }
        }

        private void ServiceAddedValueNames()
        {
            int currentNumItemsFromIVI = wpfValueAccessorList.Count;
            if (currentNumItemsFromIVI < ivi.ValueNamesArrayLength)
            {
                int maxItemsToAddPerIteration = 100;
                string[] addedNamesArray = ivi.GetValueNamesRange(currentNumItemsFromIVI, maxItemsToAddPerIteration);

                foreach (string name in addedNamesArray)
                {
                    IValueAccessor iva = ivi.GetValueAccessor(name);
                    WPFValueAccessor wva = new WPFValueAccessor(iva);
                    wpfValueAccessorList.Add(wva);
                    base[name] = wva;
                }

                // queue the generation of the event callbacks on the WPF dispatcher queue to prevent recursion from doing bad things to this structure.
                Dispatcher.CurrentDispatcher.BeginInvoke(() => GenerateCollectionIncreasedEvents(addedNamesArray));
            }
        }

        private void GenerateCollectionIncreasedEvents(string[] addedNamesArray)
        {
            if (CollectionChanged != null)
            {
                foreach (string name in addedNamesArray)
                {
                    CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, name));
                }
            }

            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("Count"));
                PropertyChanged(this, new PropertyChangedEventArgs("Item[]"));
                PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
                PropertyChanged(this, new PropertyChangedEventArgs("Values"));
            }
        }
    }


    #endregion

    #region ValueContainerTrackingDictionary

    /// <summary>
    /// This is a special case Dictionary that is used to collect and track a set of interconnect values.  
    /// This class inherits from Dictionary{string, ValueContainer} where the keys and values come from the set of IValueAccessors found by this component at construction time.
    /// After the initial setup is complete the client is expected to use the UpdateIfNeeded method(s) to scan for updated values, propagate their values into the dictionary and
    /// potentially publish a cloned dictionary, typically into a DependencyProperty using the UpdateIfNeeded publishAction delegate to consume and publish a clone of the underlying
    /// dictionary contents.
    /// </summary>
    [Obsolete("The use of this object is now obsolete.  It should be replaced with the use of the WPFValueInterconnectAdapter (2015-05-14)")]
    public class ValueContainerTrackingDictionary : Dictionary<string, ValueContainer>
    {
        /// <summary>
        /// Constructor.  Finds the set of currently defined interconnect values, obtains a corresponding set of IValueAccessors from the default interconnect value instance
        /// and builds the underlying dictionary keys and values from this set of IValueAccessor's Names and ValueContainer values.
        /// </summary>
        public ValueContainerTrackingDictionary(params String[] matchPrefixStringArray)
            : this(Modular.Interconnect.Values.Values.Instance, matchPrefixStringArray)
        {
        }

        /// <summary>
        /// Constructor.  Finds the set of currently defined interconnect values, obtains a corresponding set of IValueAccessors from the desired interconnect value instance
        /// and builds the underlying dictionary keys and values from this set of IValueAccessor's Names and ValueContainer values.
        /// </summary>
        public ValueContainerTrackingDictionary(Modular.Interconnect.Values.IValuesInterconnection valuesInterconnect, params String[] matchPrefixStringArray)
        {
            string[] valueNames = valuesInterconnect.ValueNamesArray;

            string[] filteredNames = valueNames.Where((s) => matchPrefixStringArray.Any((p) => s.StartsWith(p))).ToArray();

            ivaArray = filteredNames.Select((n) => valuesInterconnect.GetValueAccessor(n)).ToArray();

            foreach (IValueAccessor iva in ivaArray)
            {
                this[iva.Name] = iva.ValueContainer;
            }
        }

        /// <summary>
        /// Retains an array of the IValueAccessors that were found by the constructor.
        /// </summary>
        private IValueAccessor[] ivaArray;

        /// <summary>
        /// Scans through all of the IValueAccessors that were found at construction time to determine if any of them need to be updated.
        /// Updates each of these IValueAccessors and then updates the underlyng dictionary by replacing the ValueContainer for the corresponding name.
        /// <para/>Returns true if at least one IValueAccessor was updated, returns false if no IValueAccessors needed to be updated.
        /// </summary>
        public bool UpdateIfNeeded()
        {
            bool anyDeltas = false;

            foreach (var iva in ivaArray)
            {
                if (iva.IsUpdateNeeded)
                {
                    iva.Update();
                    anyDeltas = true;
                    this[iva.Name] = iva.ValueContainer;
                }
            }

            return anyDeltas;
        }

        /// <summary>
        /// Uses the parameterless UpdateIfNeeded method to update the IValueAccessors and underlying dictionary if needed.
        /// If any IValueAccessor was updated then this method generates a clone of the underlying dictionary and invokes the given publishAction with this cloned dictionary and finally returns true.
        /// If no IValueAccessor needed to be udpated then this method simply returns false.
        /// </summary>
        public bool UpdateIfNeeded(Action<Dictionary<string, ValueContainer>> publishAction)
        {
            bool updateWasNeeded = UpdateIfNeeded();
            if (updateWasNeeded && publishAction != null)
            {
                Dictionary<string, ValueContainer> clone = new Dictionary<string, ValueContainer>(this);
                publishAction(clone);
            }

            return updateWasNeeded;
        }
    }

    #endregion
}
