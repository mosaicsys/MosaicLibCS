//-------------------------------------------------------------------
/*! @file ProviderFacet.cs
 * @brief This file contains the definitions and classes that are used to define the Service Provider side, or facet, of the Modular Action portions of this library.
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2006 Mosaic Systems Inc., All rights reserved. (C++ library version)
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

namespace MosaicLib.Modular.Action
{
	//-------------------------------------------------

	/// <summary>
	/// This interface defines the common Provider side portions of the Action implementation object.
	/// The methods defined in this interface are used by the Action Queue and by generic provider base classes.
	/// </summary>
	public interface IProviderFacet
	{
        /// <summary>Provider invokes this to dispatch the mark the action as issued and invoke its delegate method.</summary>
        void IssueAndInvokeAction();

        /// <summary>Provider invokes this to internally indicate that the action should be canceled.</summary>
        void RequestCancel();

        /// <summary>Property allows provider to determine if the action cancel request has been set.</summary>
        bool IsCancelRequestActive { get; }

        /// <summary>Property gives access to the dynamically updating IActionState for this action</summary>
        IActionState ActionState { get; }

        /// <summary>Provider invokes this to replace the ActionState's NamedValues and inform action's clients of the new values</summary>
        void UpdateNamedValues(Common.NamedValueList namedValueList);

        /// <summary>Provider invokes this to indicate that the action is complete and to provide the final resultCode</summary>
        void CompleteRequest(string resultCode);

        /// <summary>Provider invokes this to indicate that the action is complete and to provide the final resultCode and set of NamedValues</summary>
        void CompleteRequest(string resultCode, Common.NamedValueList namedValueList);
    }

	//-------------------------------------------------
}

//-------------------------------------------------
