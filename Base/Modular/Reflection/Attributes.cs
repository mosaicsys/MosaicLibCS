//-------------------------------------------------------------------
/*! @file .cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.  All rights reserved
 * Copyright (c) 2012 Mosaic Systems Inc.  All rights reserved
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

namespace MosaicLib.Modular.Reflection
{
    namespace Attributes
    {
        #region Attribute related definitions

        /// <summary>
        /// This is the base class for all custom <see cref="ItemInfo"/> and TItemAttribute classes that may be used here.
        /// <para/>Provides a Name property and acts as the base class for attribute types that the AccessHelper can process
        /// </summary>
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
        public class AnnotatedItemAttributeBase : System.Attribute
        {
            /// <summary>Default constructor.</summary>
            public AnnotatedItemAttributeBase() 
                : base() 
            {
                Name = null; 
            }

            /// <summary>
            /// This property allows the attribute definition to set the item Name that should be used in the type definition for this item.  
            /// Use null or empty to use the field or property name as the item name.
            /// </summary>
            public virtual string Name { get; set; }
        }

        /// <summary>
        /// Defines which public properties and/or fields are included in the ValueSet representation 
        /// As a flag enum, user may combine enum values using the or operator "|"
        /// </summary>
        [Flags]
        public enum ItemSelection
        {
            /// <summary>Set is empty (Default value)</summary>
            None = 0x00,
            /// <summary>Set items include all public fields or properties that carry an Attributes.Item attribute.</summary>
            IncludeExplicitPublicItems = 0x01,
            /// <summary>ValueSet items include all public properties.</summary>
            IncludeAllPublicProperties = 0x02,
            /// <summary>ValueSet items include all public fields.</summary>
            IncludeAllPublicFields = 0x04,
            /// <summary>items include all fields or properties that carry an Attrirbute.Item attribute.</summary>
            IncludeExplicitItems = 0x08,
        }

        /// <summary>
        /// Defines the access type that is supported by the included items in a class that marked as Attributes.Serializable.
        /// </summary>
        [Flags]
        public enum ItemAccess
        {
            /// <summary>ValueSet is not accessible (Default value)</summary>
            None = 0x00,
            /// <summary>ValueSet items can be read/serialized</summary>
            Read = 0x01,
            /// <summary>ValueSet items can be written/deserialized</summary>
            Write = 0x02,
            /// <summary>ValueSet items can be read/serialized and can be written/deserialized</summary>
            ReadWrite = (Read | Write),
        }

        #endregion

        #region ItemInfo

        /// <summary>
        /// Container class for information that is extracted from an class using the AnnotatedClassItemAccessListExtractor class
        /// </summary>
        public class ItemInfo
        {
            /// <summary>Defines the property/field type of the related property or field</summary>
            public Type ItemType { get; set; }

            /// <summary>Gives the annotated attribute as a System.Attribute</summary>
            public System.Attribute ItemAttribute { get; set; }	            // may be null if property or field does not carry any Item Attribute...

            /// <summary>Gives the PropertyInfo of the corresponding selected property member from the original class</summary>
            /// <remarks>only one of PropertyInfo or FieldInfo will be non-null</remarks>
            public PropertyInfo PropertyInfo { get; set; }

            /// <summary>Gives the FieldInfo of the corresponding selected field member from the original class</summary>
            /// <remarks>only one of PropertyInfo or FieldInfo will be non-null</remarks>
            public FieldInfo FieldInfo { get; set; }

            /// <summary>Returns true if the selected item is a property</summary>
            public bool IsProperty { get { return (PropertyInfo != null); } }
            /// <summary>Returns true if the selected item is a field</summary>
            public bool IsField { get { return (FieldInfo != null); } }
            /// <summary>Returns the PropertyInfo or FieldInfo as a MemberInfo (for access to common sub-properties such as Name</summary>
            public MemberInfo MemberInfo { get { return (IsProperty ? PropertyInfo as MemberInfo : FieldInfo as MemberInfo); } }

            /// <summary>True if the item can get the member value.  True for all fields and for properties that CanRead.</summary>
            public bool CanGetValue { get { return (IsField || (IsProperty && PropertyInfo.CanRead)); } }
            /// <summary>True if the item can set the member value.  True for all fields and for properties that CanWrite.</summary>
            public bool CanSetValue { get { return (IsField || (IsProperty && PropertyInfo.CanWrite)); } }
        }

        /// <summary>
        /// Container class for information that is extracted from an class using the AnnotatedClassItemAccessListExtractor class
        /// </summary>
        public class ItemInfo<TItemAttribute>
            : ItemInfo
            where TItemAttribute : AnnotatedItemAttributeBase, new()
        {
            private TItemAttribute itemAttribute;

            /// <summary>Replaces ItemInfo base Attribute with derived type specific version.</summary>
            /// <remarks>may be null if property or field does not carry any Item Attribute...</remarks>
            public new TItemAttribute ItemAttribute { get { return itemAttribute; } set { itemAttribute = value; base.ItemAttribute = value; } }

            /// <summary>Returns the ItemAtribute's Name if it is non-null or returns the MemberInfo's Name</summary>
            public string DerivedName
            {
                get
                {
                    if (ItemAttribute != null && ItemAttribute.Name != null)
                        return ItemAttribute.Name;
                    return MemberInfo.Name;
                }
            }
        }

        #endregion

        #region AnnotatedClassItemAccessHelper

        /// <summary>
        /// static "Namespace" class for static methods used to generate getter Func's and setter Action's from MosaicLib.Modular.Reflection.Attributes.ItemInfo objects 
        /// </summary>
        public static class AnnotatedClassItemAccessHelper
        {
            /// <summary>
            /// static Factory method used to generate a getter Func for a Property or Field as identified by the contents of the given ItemInfo object.
            /// This Property or Field must be accessible to the generated code or the first attempt to perform the get will throw a security exception.
            /// </summary>
            /// <typeparam name="TAnnotatedClass">Gives the type of the class on which the getter will be evaluated and from which the ItemInfo was harvisted.</typeparam>
            /// <typeparam name="TValueType">Gives the value type returned by the getter.</typeparam>
            /// <param name="item">Gives the PropertyInfo and/or FieldInfo for the item that shall be used to generate a getter Function.</param>
            /// <returns>a System.Func{TAnnotatedClass, TValueType} object that may be invoked later obtain the value of the selected field or property from an instance of the TAnnotatedClass that is given to the Func when it is invoked.</returns>
            public static Func<TAnnotatedClass, TValueType> GenerateGetter<TAnnotatedClass, TValueType>(ItemInfo item)
            {
                Func<TAnnotatedClass, TValueType> func = null;

                if (item.IsField)
                    func = Reflection.GetterSetterFactory.CreateFieldGetterExpression<TAnnotatedClass, TValueType>(item.FieldInfo);
                else
                    func = Reflection.GetterSetterFactory.CreatePropertyGetterFunc<TAnnotatedClass, TValueType>(item.PropertyInfo);

                return func;
            }

            /// <summary>
            /// static Factory method used to generate a setter Action for a Property or Field as identified by the contents of the ItemInfo object.
            /// This Property or Field must be accessible to the generated code or the first attempt to perform the set will throw a security exception.
            /// </summary>
            /// <typeparam name="TAnnotatedClass">Gives the type of the class on which the setter will be performed and from which the ItemInfo was harvisted.</typeparam>
            /// <typeparam name="TValueType">Gives the value type accepted by the setter.</typeparam>
            /// <param name="item">Gives the PropertyInfo and/or FieldInfo for the item that shall be used to generate a getter Function.</param>
            /// <returns>a System.Action{TAnnotatedClass, TValueType} object that may be invoked later to assign a value to the selected field or propety in an instance of the TAnnotatedClass that is given to the Action when it is invoked.</returns>
            public static Action<TAnnotatedClass, TValueType> GenerateSetter<TAnnotatedClass, TValueType>(ItemInfo item)
            {
                Action<TAnnotatedClass, TValueType> action = null;

                if (item.IsField)
                    action = Reflection.GetterSetterFactory.CreateFieldSetterAction<TAnnotatedClass, TValueType>(item.FieldInfo);
                else
                    action = Reflection.GetterSetterFactory.CreatePropertySetterAction<TAnnotatedClass, TValueType>(item.PropertyInfo);

                return action;
            }
        }

        /// <summary>
        /// Templatized helper class used to facilitate use of attributes derived from <see cref="AnnotatedItemAttributeBase"/> including extracting them from 
        /// an annotated class and to help generate accessor functions for these annotated items.
        /// </summary>
        /// <typeparam name="TItemAttribute"></typeparam>
        public static class AnnotatedClassItemAccessHelper<TItemAttribute> where TItemAttribute : AnnotatedItemAttributeBase, new()
        {
            /// <summary>This method is used to extract a set of ItemInfo objects from the given annotatedClassType and for its items that are defined by the given itemSelection.</summary>
            /// <param name="annotatedClassType">The class type that the caller would like to extract, nominally annotated, items from.</param>
            /// <param name="itemSelection">The types of items that the caller would like to get information about.</param>
            /// <returns>
            /// A Dictionary of ItemInfo objects templatized on the helper's templatized TItemAttribute attribute type.  
            /// The Dictionary Keys are the ItemInfo.DerivedName values while the Dictionary Values are the ItemInfo objects themselves.
            /// If two ItemInfo items share the same DerivedName value then the Dictionary will only contain the first such ItemInfo and will ignore the later ones.
            /// </returns>
            public static Dictionary<string, ItemInfo<TItemAttribute>> ExtractItemInfoAccessDictionaryFrom(Type annotatedClassType, ItemSelection itemSelection)
            {
                List<ItemInfo<TItemAttribute>> itemList = ExtractItemInfoAccessListFrom(annotatedClassType, itemSelection);

                Dictionary<string, ItemInfo<TItemAttribute>> itemDictionary = new Dictionary<string, ItemInfo<TItemAttribute>>();

                foreach (ItemInfo<TItemAttribute> item in itemList)
                {
                    if (itemDictionary.ContainsKey(item.DerivedName))
                        continue;
                    itemDictionary.Add(item.DerivedName, item);
                }

                return itemDictionary;
            }

            /// <summary>This method is used to extract a list of ItemInfo objects from the given annotatedClassType and for its items that are defined by the given itemSelection.</summary>
            /// <param name="annotatedClassType">The class type that the caller would like to extract, nominally annotated, items from.</param>
            /// <param name="itemSelection">The types of items that the caller would like to get information about.</param>
            /// <returns>A list of ItemInfo objects templatized on the helper's templatized TItemAttribute attribute type.</returns>
            public static List<ItemInfo<TItemAttribute>> ExtractItemInfoAccessListFrom(Type annotatedClassType, ItemSelection itemSelection)
            {
                Type ItemAttributeType = typeof(TItemAttribute);
                string typeName = annotatedClassType.ToString();

                object[] attribArray = null;

                bool includeExplicitItems = ((itemSelection & ItemSelection.IncludeExplicitItems) != ItemSelection.None);
                bool includeExplicitPublicItems = ((itemSelection & (ItemSelection.IncludeExplicitPublicItems | ItemSelection.IncludeExplicitItems)) != ItemSelection.None);
                bool includeAllPublicProperties = ((itemSelection & ItemSelection.IncludeAllPublicProperties) != ItemSelection.None);
                bool includeAllPublicFields = ((itemSelection & ItemSelection.IncludeAllPublicFields) != ItemSelection.None);

                List<ItemInfo<TItemAttribute>> itemAccessInfoList = new List<ItemInfo<TItemAttribute>>();

                PropertyInfo[] piSet = annotatedClassType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (PropertyInfo pi in piSet)
                {
                    bool canRead = pi.CanRead;
                    bool canWrite = pi.CanWrite;
                    Type piType = pi.PropertyType;
                    bool isPublic = (pi.GetGetMethod() ?? pi.GetSetMethod()).IsPublic;

                    attribArray = pi.GetCustomAttributes(ItemAttributeType, false);
                    TItemAttribute ia = (attribArray.Length == 1 ? (attribArray[0] as TItemAttribute) : null);

                    bool includeThisPublicProperty = (isPublic && (includeAllPublicProperties || (ia != null && includeExplicitPublicItems)));
                    bool includeThisExplicitProperty = (!isPublic && (ia != null && includeExplicitItems));
                    if (!includeThisPublicProperty && !includeThisExplicitProperty)
                        continue;		// skip properties that are not selected for inclusion

                    // save relevant parts in list
                    ItemInfo<TItemAttribute> iai = new ItemInfo<TItemAttribute>();
                    iai.ItemType = piType;
                    iai.ItemAttribute = ia;
                    iai.PropertyInfo = pi;

                    itemAccessInfoList.Add(iai);
                }

                FieldInfo[] fiSet = annotatedClassType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (FieldInfo fi in fiSet)
                {
                    string fieldName = fi.Name;
                    Type fiType = fi.FieldType;
                    bool isPublic = fi.IsPublic;

                    attribArray = fi.GetCustomAttributes(ItemAttributeType, false);
                    TItemAttribute ia = (attribArray.Length == 1 ? (attribArray[0] as TItemAttribute) : null);

                    bool includeThisPublicField = (isPublic && (includeAllPublicFields || (ia != null && includeExplicitPublicItems)));
                    bool includeThisExplicitField = (!isPublic && (ia != null && includeExplicitItems));
                    if (!includeThisPublicField && !includeThisExplicitField)
                        continue;		// skip fields that are not selected for inclusion

                    // save relevant parts in list
                    ItemInfo<TItemAttribute> iai = new ItemInfo<TItemAttribute>();
                    iai.ItemType = fiType;
                    iai.ItemAttribute = ia;
                    iai.FieldInfo = fi;

                    itemAccessInfoList.Add(iai);
                }

                return itemAccessInfoList;
            }
        }

        #endregion
    }
}

//-------------------------------------------------------------------
