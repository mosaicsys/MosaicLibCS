//-------------------------------------------------------------------
/*! @file ObjID.cs
 * @brief This file defines an ObjIDBase class that is intended to simplify construction of objects that need an ObjID.
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

namespace MosaicLib.Utils
{
	using System;

    /// <summary>
    /// A very simple interface that is implemented by things that have an ObjID
    /// </summary>
	public interface IObjID
	{
		string ObjID { get; }
	}

    /// <summary>
    /// Common simple base class that can be used to implement the IObjID interface either by inheritance or agregation.
    /// </summary>
	public class ObjIDBase : IObjID
	{
		private string objID = string.Empty;
		public string ObjID { get { return objID; } }
		private string className = string.Empty;
		public string ClassName { get { return ClassName; } }

        public ObjIDBase(string objID) : this(string.Empty, objID, string.Empty) { }
		public ObjIDBase(string className, string objID) : this (className, objID, string.Empty) {}
		public ObjIDBase(string className, string objID, string objIDStrSuffix)
		{
            if (string.IsNullOrEmpty(className))
                className = new System.Diagnostics.StackFrame(1).GetType().Name;    // get the name of the caller's type

            this.className = className;

            if (string.IsNullOrEmpty(objIDStrSuffix))
				objIDStrSuffix = string.Empty;

			if (!string.IsNullOrEmpty(objID))
				this.objID = objID + objIDStrSuffix;

			if (string.IsNullOrEmpty(objID))
			{
				object obj = this;
				string objHashCodeStr = obj.GetHashCode().ToString("x8");

				objID = Utils.Fcns.CheckedFormat("{0}{1}", objHashCodeStr, objIDStrSuffix);
			}
		}
	}

    /// <summary>This clase helps generate base64 versions of GUIDs for use as short objectIDs</summary>
    public static class GuidB64
    {
        /// <summary>Returns the Base64 version of a new GUID as produced by call to System.Guid.NewGuid()</summary>
        public static string NewID { get { return ToBase64(System.Guid.NewGuid()); } }

        /// <summary>Returns the Base64 version of an empty GUID (byte array of all zeros)</summary>
        public static string EmptyID { get { return emptyID; } }

        /// <summary>Returns the Base64 version of the given guid.</summary>
        public static string ToBase64(System.Guid guid)
        {
            byte[] guidByteArray = guid.ToByteArray();
            return Convert.ToBase64String(guidByteArray);
        }

        /// <summary>static storage for the precomputed emptyID that is returned by the Empty property.</summary>
        private static string emptyID = ToBase64(System.Guid.Empty);
    }
}

//-------------------------------------------------------------------
