//-------------------------------------------------------------------
/*! @file Commands.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2019 Mosaic Systems Inc.
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

using System.Windows.Input;

namespace MosaicLib.WPF.Commands
{
    public static class Commands
    {
        /// <summary>
        /// This RoutedCommand is generally intended to be used with buttons and menu items that generate and start/run service actions on target parts.
        /// The CommandParameter and Attachable.NPV properties are generally used with the command.  
        /// The NPV attached property may generally be applied to/found on the corresponding event source or event original source object.
        /// The CancellationGroup attached/attachable property may also be used to define the cancellation group that this command is associated with (see RouedCancelServiceCommand).
        /// <para/>This class is generally used with a Window's CommandBindings property to define the linkage of this comman instance to a specific Executed event handler.
        /// </summary>
        public static RoutedCommand RoutedServiceCommand
        {
            get { return _routedServiceCommand ?? (_routedServiceCommand = new RoutedCommand("RoutedServiceCommand", typeof(Commands))); }
        }

        /// <summary>
        /// This RoutedCommand is generally intended to be used with buttons and menu items that generate the desired to cancel one or more recently
        /// created service actions (see RoutedServiceCommand class above).  The general intention is that the use of this command attempts is expected to cancel
        /// one or more outstanding (and incomplete) service actions that were created using the RoutedServiceCommand command and routed event handler.
        /// By context the the routed event handler for this command is expected ot make use of the CommandParameter and/or other attached properties in order to
        /// select the set of outstanding (aka incomplete) service actions that are to be canceled.
        /// For example the CancellationGroup attached/attachable property may be used to support grouping service actions and then this command's handler could use the
        /// CommandParameter to define the group selection that is to be canceled.
        /// </summary>
        public static RoutedCommand RoutedCancelServiceCommand
        {
            get { return _routedCancelServiceCommand ?? (_routedCancelServiceCommand = new RoutedCommand("RoutedCancelServiceCommand", typeof(Commands))); }
        }

        private static RoutedCommand _routedServiceCommand, _routedCancelServiceCommand;
    }
}

