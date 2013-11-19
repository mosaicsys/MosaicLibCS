//-------------------------------------------------------------------
/*! @file MosaicLibCSBase.cs
 *  @brief This file contains definitions that are shared by various subparts of the MosaicLib namespace
 * 
 * Copyright (c) Mosaic Systems Inc.  All rights reserved
 * Copyright (c) 2008 Mosaic Systems Inc.  All rights reserved
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

namespace MosaicLib
{
    using System;
    using System.Collections.Generic;

    using System.ServiceModel;
    using System.Runtime.Serialization;

    /// <summary>
    /// Defines a series of constants for this library, including common namespace urn's used within the project.
    /// </summary>
    public static partial class Constants
    {
        /// <summary>urn://mosaicsys.com/NS</summary>
        public const string MosaicSysNameSpaceRoot = "urn://mosaicsys.com/NS";

        /// <summary>urn://mosaicsys.com/NS/MLib</summary>
        public const string MosaicLibNameSpaceRoot = MosaicSysNameSpaceRoot + "/MLib";

        /// <summary>urn://mosaicsys.com/NS/MLib.Logging</summary>
        public const string LoggingNameSpace = MosaicLibNameSpaceRoot + ".Logging";

        /// <summary>urn://mosaicsys.com/NS/MLib.Modular</summary>
        public const string ModularNameSpace = MosaicLibNameSpaceRoot + ".Modular";

        /// <summary>urn://mosaicsys.com/NS/MLib.Semi</summary>
        public const string SemiNameSpace = MosaicLib.Constants.MosaicLibNameSpaceRoot + ".Semi";

        /// <summary>urn://mosaicsys.com/NS/MLib.Semi.E005</summary>
        public const string E005NameSpace = MosaicLib.Constants.SemiNameSpace + ".E005";

        /// <summary>urn://mosaicsys.com/NS/MLib.Semi.E004</summary>
        public const string E084NameSpace = SemiNameSpace + ".E084";
    }
}

//-------------------------------------------------------------------
