//-------------------------------------------------------------------
/*! @file Interconnect/Parts.cs
 *  @brief Defines a set of classes that are used to supported use to interconnect parts by part name and allow them to create service actions by part name.
 * 
 * Copyright (c) Mosaic Systems Inc.,  All rights reserved.
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
//-------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using MosaicLib.Utils;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Part;

// Modular.Interconnect is the general namespace for tools that help interconnect Modular Parts without requiring that that have pre-existing knowledge of each-other's classes.
// This file contains the definitions for the underlying Modular.Interconnect.Actions namespace.
//  
//  Modular.Interconnect.Parts provides a set of tools that are used to define one or more table spaces for interconnected parts.  
//  These are implementations of the IPartsInterconnection interface that defines what capabilities such objects expose to client code.
//  
//  The Parts static class contains a singleton Instance pattern so that there can always be a main value interconnection instance that other objects can use.  However all of the
//  helper classes that are used with IPartsInterconnection objects support use with any externally provided instance so the client process can support multiple separate
//  Parts Interconnection tables if needed.
//
//  IPartsInterconnection objects also serve as a form of dynamic factory for actions (currently only Service Actions) where the actual action is created by the selected part (or a proxy for it).

namespace MosaicLib.Modular.Interconnect.Parts
{
    #region IPartsInterconnection

    /// <summary>
    /// This interface defines the basic externally visible methods and properties that are exposed by Parts Interconnection implementation objects.
    /// </summary>
    public interface IPartsInterconnection
    {
        /// <summary>Returns the name of this actions interconnection (table space) object.</summary>
        string Name { get; }

        /// <summary>
        /// Attempt to find a registered part and return it.
        /// If the desired part cannot be found then the method returns null (if throwOnNotFound is false) or it throws a PartIDNotFoundException (if the throwOnNotFound is true).
        /// </summary>
        /// <param name="partID">Gives the part ID on which to create a service action.</param>
        /// <param name="throwOnNotFound">When true this method will throw a PartIDNotFoundException if the given partID is not found.  When false the method will return null when the given partID is not found.</param>
        /// <exception cref="PartIDNotFoundException">Thrown when the partID is not found and throwOnNotFound is given as true.</exception>
        IActivePartBase FindPart(string partID, bool throwOnNotFound);

        /// <summary>
        /// Attempt to find a registered part and then asks it to create a service action with the given initial serviceName parameter value.
        /// If the desired part cannot be found then the method returns null (if throwOnNotFound is false) or it throws a PartIDNotFoundException (if the throwOnNotFound is true).
        /// </summary>
        /// <param name="partID">Gives the part ID on which to create a service action.</param>
        /// <param name="serviceName">Gives the initial value of the service name to be performed, or null, or string.Empty if the name is not already known.</param>
        /// <param name="throwOnNotFound">When true this method will throw a PartIDNotFoundException if the given partID is not found.  When false the method will return null when the given partID is not found.</param>
        /// <exception cref="PartIDNotFoundException">Thrown when the partID is not found and throwOnNotFound is given as true.</exception>
        IStringParamAction CreateServiceAction(string partID, string serviceName, bool throwOnNotFound);

        /// <summary>
        /// Attempts to register the given part as an available target for creation of Interconnected Actions
        /// </summary>
        void RegisterPart(IActivePartBase part);
    }

    /// <summary>
    /// Exception that is throw by IActinsInterconnection.CreateServiceAction when throwOnNotFound is true and the given partID is not found.
    /// </summary>
    public class PartIDNotFoundException : System.Exception
    {
        /// <summary>
        /// Constructor.  Caller provides a string mesg and an optional innerException (or null if there is none).
        /// </summary>
        public PartIDNotFoundException(string mesg, System.Exception innerException) : base(mesg, innerException) {}
    }

    #endregion

    #region static Parts namespace for Interconnection singleton Instance

    /// <summary>
    /// This is a static class that serves as the namespace within which to place the IPartsInterconnection singleton that is commonly used as the main actions interconnection instance.
    /// </summary>
    public static class Parts
    {
        #region Singleton instance

        /// <summary>Gives the caller get and set access to the singleton IPartsInterconnection instance that is used to provide application wide action interconnection.</summary>
        public static IPartsInterconnection Instance
        {
            get { return singletonInstanceHelper.Instance; }
            set { singletonInstanceHelper.Instance = value; }
        }

        private static SingletonHelperBase<IPartsInterconnection> singletonInstanceHelper = new SingletonHelperBase<IPartsInterconnection>(SingletonInstanceBehavior.AutoConstructIfNeeded, () => new PartsInterconnection("MainActionsInterconnect"));

        #endregion
    }

    #endregion

    #region PartsInterconnection implementation

    /// <summary>
    /// This is the primary implementation class for the IPartsInterconnection interface.
    /// </summary>
    public class PartsInterconnection : IPartsInterconnection
    {
        #region construction

        /// <summary>Constructor.  Requires an instance name.</summary>
        public PartsInterconnection(string name)
        {
            Name = name;
            Logger = new Logging.Logger(name);
        }

        #endregion

        #region IPartsInterconnection interface

        /// <summary>Returns the name of this values interconnection (table space) object.</summary>
        public string Name { get; private set; }

        /// <summary>
        /// Attempt to find a registered part and return it.
        /// If the desired part cannot be found then the method returns null (if throwOnNotFound is false) or it throws a PartIDNotFoundException (if the throwOnNotFound is true).
        /// </summary>
        /// <param name="partID">Gives the part ID on which to create a service action.</param>
        /// <param name="throwOnNotFound">When true this method will throw a PartIDNotFoundException if the given partID is not found.  When false the method will return null when the given partID is not found.</param>
        /// <exception cref="PartIDNotFoundException">Thrown when the partID is not found and throwOnNotFound is given as true.</exception>
        public IActivePartBase FindPart(string partID, bool throwOnNotFound)
        {
            IActivePartBase part = null;

            if (partID != null)
            {
                lock (mutex)
                {
                    partIDDictionary.TryGetValue(partID, out part);
                }
            }

            if (part == null && throwOnNotFound)
                throw new PartIDNotFoundException("PartID '{0}' was not found in Action Interconnection '{1}'".CheckedFormat(partID, Name), null);

            return part;
        }

        /// <summary>
        /// Attempt to find a registered part and then asks it to create a service action with the given initial serviceName parameter value.
        /// If the desired part cannot be found then the method returns null (if throwOnNotFound is false) or it throws a PartIDNotFoundException (if the throwOnNotFound is true).
        /// </summary>
        /// <param name="partID">Gives the part ID on which to create a service action.</param>
        /// <param name="serviceName">Gives the initial value of the service name to be performed, or null, or string.Empty if the name is not already known.</param>
        /// <param name="throwOnNotFound">When true this method will throw a PartIDNotFoundException if the given partID is not found.  When false the method will return null when the given partID is not found.</param>
        /// <exception cref="PartIDNotFoundException">Thrown when the partID is not found and throwOnNotFound is given as true.</exception>
        public IStringParamAction CreateServiceAction(string partID, string serviceName, bool throwOnNotFound)
        {
            IActivePartBase part = FindPart(partID, throwOnNotFound);

            if (part != null)
                return part.CreateServiceAction(serviceName);
            else
                return null;
        }

        /// <summary>
        /// Attempts to register the given part as an available target for creation of Interconnected Actions
        /// </summary>
        public void RegisterPart(IActivePartBase part)
        {
            IActivePartBase replacedPart = null;

            if (part != null && !part.PartID.IsNullOrEmpty())
            {
                string partID = part.PartID;
                lock (mutex)
                {
                    partIDDictionary.TryGetValue(partID, out replacedPart);
                    partIDDictionary[partID] = part;
                }
            }

            if (replacedPart != null)
            {
                if (!object.ReferenceEquals(part, replacedPart))
                    Logger.Debug.Emit("Registration of PartID '{0}' replaced a previously registered part of the same name", part.PartID);
                else
                    Logger.Debug.Emit("Redundant registration encountered for PartID '{0}'", part.PartID);
            }
        }

        #endregion

        #region private Table and table space related implementation fields

        protected Logging.Logger Logger { get; private set; }

        /// <summary>
        /// table space and dictionary mutex.  
        /// </summary>
        private object mutex = new object();

        /// <summary>This is the dictionary that is used to convert part ID's into the corresponding IActivePartBase registered for that name.</summary>
        private Dictionary<string, IActivePartBase> partIDDictionary = new Dictionary<string, IActivePartBase>();

        #endregion

    }

    #endregion
}

//-------------------------------------------------------------------
