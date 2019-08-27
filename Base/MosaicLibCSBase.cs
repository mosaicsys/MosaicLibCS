//-------------------------------------------------------------------
/*! @file MosaicLibCSBase.cs
 *  @brief This file contains definitions that are shared by various subparts of the MosaicLib namespace
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2008 Mosaic Systems Inc.
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
using System.Collections.Generic;

using System.ServiceModel;
using System.Runtime.Serialization;

namespace MosaicLib
{
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

        /// <summary>urn://mosaicsys.com/NS/MLib.Utils</summary>
        public const string UtilsNameSpace = MosaicLibNameSpaceRoot + ".Utils";

        /// <summary>urn://mosaicsys.com/NS/MLib.Modular</summary>
        public const string ModularNameSpace = MosaicLibNameSpaceRoot + ".Modular";

        /// <summary>urn://mosaicsys.com/NS/MLib.Modular.Common</summary>
        public const string ModularCommonNameSpace = ModularNameSpace + ".Common";

        /// <summary>urn://mosaicsys.com/NS/MLib.Modular</summary>
        public const string ModularActionNameSpace = ModularNameSpace + ".Action";

        /// <summary>urn://mosaicsys.com/NS/MLib.Modular.InterConn</summary>
        public const string ModularInterconnectNameSpace = ModularNameSpace + ".InterConn";

        /// <summary>urn://mosaicsys.com/NS/MLib.Modular.Config</summary>
        public const string ConfigNameSpace = ModularNameSpace + ".Config";

        /// <summary>urn://mosaicsys.com/NS/MLib.Semi</summary>
        public const string SemiNameSpace = MosaicLibNameSpaceRoot + ".Semi";

        /// <summary>urn://mosaicsys.com/NS/MLib.Semi.E005</summary>
        public const string E005NameSpace = SemiNameSpace + ".E005";

        /// <summary>urn://mosaicsys.com/NS/MLib.Semi.E039</summary>
        public const string E039NameSpace = SemiNameSpace + ".E039";

        /// <summary>urn://mosaicsys.com/NS/MLib.Semi.E040</summary>
        public const string E040NameSpace = SemiNameSpace + ".E040";

        /// <summary>urn://mosaicsys.com/NS/MLib.Semi.E004</summary>
        public const string E084NameSpace = SemiNameSpace + ".E084";

        /// <summary>urn://mosaicsys.com/NS/MLib.Semi.E087</summary>
        public const string E087NameSpace = SemiNameSpace + ".E087";

        /// <summary>urn://mosaicsys.com/NS/MLib.Semi.E0890</summary>
        public const string E090NameSpace = SemiNameSpace + ".E090";

        /// <summary>urn://mosaicsys.com/NS/MLib.Semi.E094</summary>
        public const string E094NameSpace = SemiNameSpace + ".E094";

        /// <summary>urn://mosaicsys.com/NS/MLib.PartsLib</summary>
        public const string PartsLibNameSpace = MosaicLibNameSpaceRoot + ".PartsLib";
    }
}

//-------------------------------------------------------------------
