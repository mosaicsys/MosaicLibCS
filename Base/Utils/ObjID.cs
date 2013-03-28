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
        // Gives the "Name" or Object ID of the current object.
		string ObjID { get; }
	}

    /// <summary>
    /// Common simple base class that can be used to implement the IObjID interface either by inheritance or agregation.
    /// </summary>
	public class ObjIDBase : IObjID
	{
        /// <summary>Public implementation for the IObjID.ObjID property.</summary>
        public string ObjID { get; private set; }

        /// <summary>Carries the "ClassName" that was given at construction time.  Will either be the actual source code class name or some shorthand version thereof.</summary>
        public string ClassName { get; private set; }

        /// <summary>Basic constructor.  Takes ClassName from class name of caller.  Uses empty suffix.</summary>
        public ObjIDBase(string objID) : this(1, objID, string.Empty) { }
        /// <summary>Basic constructor.  Caller specifies ObjID and ClassName.  Uses empty suffix.</summary>
        public ObjIDBase(string className, string objID) : this(className, objID, string.Empty) { }

        /// <summary>Nested constructor.  Takes ClassName from class name of caller n=skipStackFrames stack frames up from here.  Uses empty suffix.</summary>
        public ObjIDBase(int skipStackFrames, string objID) : this(new System.Diagnostics.StackFrame(skipStackFrames + 1).GetType().Name, objID, string.Empty) { }

        /// <summary>Nested constructor.  Takes ClassName from class name of caller n=skipStackFrames stack frames up from here.</summary>
        public ObjIDBase(int skipStackFrames, string objID, string objIDStrSuffix) : this(new System.Diagnostics.StackFrame(skipStackFrames + 1).GetType().Name, objID, objIDStrSuffix) { }

        /// <summary>
        /// Essential constructor: caller specifies className, objID and objIDStrSuffix.  
        /// If className is given as null or empty, method takes class name from caller's type definition.  
        /// If objID is given as null or empty, method takes 8 hex digit hash code of this object to use as ObjID.
        /// </summary>
        public ObjIDBase(string className, string objID, string objIDStrSuffix)
		{
            if (string.IsNullOrEmpty(className))
                className = new System.Diagnostics.StackFrame(1).GetType().Name;    // get the name of the caller's type

            ClassName = className;

            objIDStrSuffix = Fcns.MapNullToEmpty(objIDStrSuffix);

			if (!string.IsNullOrEmpty(objID))
				objID = objID + objIDStrSuffix;
            else
			{
				object obj = this;
				string objHashCodeStr = obj.GetHashCode().ToString("x8");

				objID = Utils.Fcns.CheckedFormat("{0}{1}", objHashCodeStr, objIDStrSuffix);
			}

            ObjID = objID;
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
