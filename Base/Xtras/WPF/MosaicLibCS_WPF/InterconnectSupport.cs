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

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Interconnect.Values;

namespace MosaicLib.WPF.Interconnect
{
    /// <summary>
    /// This is a special case Dictionary that is used to collect and track a set of interconnect values.  
    /// This class inherits from Dictionary{string, ValueContainer} where the keys and values come from the set of IValueAccessors found by this component at construction time.
    /// After the initial setup is complete the client is expected to use the UpdateIfNeeded method(s) to scan for updated values, propagate their values into the dictionary and
    /// potentially publish a cloned dictionary, typically into a DependencyProperty using the UpdateIfNeeded publishAction delegate to consume and publish a clone of the underlying
    /// dictionary contents.
    /// </summary>
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
        public ValueContainerTrackingDictionary(Modular.Interconnect.Values.IValuesIneterconnection valuesInterconnect, params String[] matchPrefixStringArray)
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
}
