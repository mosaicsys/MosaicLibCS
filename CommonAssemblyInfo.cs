//-------------------------------------------------------------------
/*! @file CommonAssemblyInfo.cs
 *  @brief This file contains the common assembly attributes for all of thee MosaicLibCS related assemblies.
 *  This file is generally included (by link) in the corresponding asseembly project files next to the assembly specific AssemblyInfo.cs file.
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

using System.Reflection;

[assembly: AssemblyProduct("MosaicLibCS")]
[assembly: AssemblyCompany("Mosaic Systems Inc.")]
[assembly: AssemblyCopyright("Copyright © Mosaic Systems Inc.  All rights reserved.  Licensed under the Apache License, Version 2.0.")]

[assembly: AssemblyInformationalVersion("0.1.7.0_PR09: Preview 09")]
[assembly: AssemblyFileVersion("0.1.7.0")]
[assembly: AssemblyVersion("0.1.7.0")]

// AssemblyInformationalVersion:  "Product Version" dipslayed in explorer version info.  Describes the version including descriptive additions to the Major.Minor.Build.Revision pattern.
// AssemblyFileVersion: "File Version" diplayed in explorer version info.
// AssemblyVersion: not displayed in explorer version info.  Use as index for assembly load time version checking (when enabled)

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
