//-------------------------------------------------------------------
/*! @file MermaidHelpers.cs
 *  @brief This file provides a set of helper definitions and extension methods for use in generating mermaid output.
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2024 Mosaic Systems Inc.
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

namespace Mosaic.ToolsLib.Mermaid.Helpers
{
    public enum MermaidSequenceDiagramArrowType : int
    {
        Invalid,
        SolidNoArrow,
        DottedNoArrow,
        SolidArrow,
        DottedArrow,
        SolidArrowAsync,
        DottedArrowAsync,
        SolidXArrow,
        DottedXArrow,
    }

    public static partial class ExtensionMethods
    {
        public static string GetArrowText(this MermaidSequenceDiagramArrowType arrowType)
        {
            switch (arrowType) 
            {
                case MermaidSequenceDiagramArrowType.SolidNoArrow: return "->";
                case MermaidSequenceDiagramArrowType.DottedNoArrow: return "->";
                case MermaidSequenceDiagramArrowType.SolidArrow: return "->>";
                case MermaidSequenceDiagramArrowType.DottedArrow: return "-->>";
                case MermaidSequenceDiagramArrowType.SolidArrowAsync: return "-)";
                case MermaidSequenceDiagramArrowType.DottedArrowAsync: return "--)";
                case MermaidSequenceDiagramArrowType.SolidXArrow: return "-x";
                case MermaidSequenceDiagramArrowType.DottedXArrow: return "-x";
                default: return $"{arrowType}_IsNotAValidArrowType";
            }
        }
    }
}
