//-------------------------------------------------------------------
/*! @file Attributes.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2012 Mosaic Systems Inc.
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
using System.Linq;
using System.Reflection;

using MosaicLib.Modular.Common;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.Modular.Reflection
{
    namespace Attributes
    {
        #region Attribute related definitions: IAnnotatedItemAttribute, AnnotatedItemAttributeBase

        /// <summary>
        /// Interface for item attribute types that may be used with the item harvesting logic supported here.
        /// </summary>
        public interface IAnnotatedItemAttribute
        {
            /// <summary>
            /// This property allows the attribute definition to set the item Name that should be used in the type definition for this item.  
            /// Use null to force use the field or property MemberInfo.Name as the derived name.
            /// <para/>Defaults to null.
            /// </summary>
            string Name { get; }

            /// <summary>
            /// This property is used to guide the generation of the derived name by choosing which strategy is to be used.
            /// <para/>Defaults to Prefix0.  Other choices are None, Format, Prefix1, Prefix2, or Prefix3
            /// </summary>
            NameAdjust NameAdjust { get; }

            /// <summary>
            /// When an item is marked to SilenceIssues, no issue messages will be emitted if the value cannot be accessed.  Value messages will still be emitted.
            /// </summary>
            bool SilenceIssues { get; }

            /// <summary>
            /// When this property is set to be any value other than None (its default), the value marshalling will attempt to cast the member value to/from this cotnainer type when setting/getting container contents.
            /// </summary>
            ContainerStorageType StorageType { get; }

            /// <summary>
            /// Used to control the access for this item.  Combine this with UseGetter and UseSetter extension methods.
            /// Default value is ItemAccess.Normal (selects both UseSetterIfPresent and UseGetterIfPresent)
            /// </summary>
            ItemAccess ItemAccess { get; }

            /// <summary>
            /// Generates a full derived name from the given memberInfo's Name, the Name property, the NameAdjust property and the given paramsStrArray contents
            /// </summary>
            string GenerateFullName(MemberInfo memberInfo, params string[] paramsStrArray);

            /// <summary>
            /// Generates a derived name from the given memberName, the Name property, the NameAdjust property and the given paramsStrArray contents
            /// </summary>
            string GenerateFullName(string memberName, params string[] paramsStrArray);
        }

        /// <summary>
        /// This is the base class for all custom <see cref="ItemInfo"/> and TItemAttribute classes that may be used here.
        /// <para/>Provides a Name property and acts as the base class for attribute types that the AccessHelper can process.
        /// <para/>Name property defaults to null, NameAdjust property defaults to NameAdjust.Prefix0
        /// <para/>AdditionalKeywords = empty string array.
        /// </summary>
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
        public class AnnotatedItemAttributeBase : System.Attribute, IAnnotatedItemAttribute
        {
            /// <summary>
            /// Default constructor.
            /// <para/>Sets Name = null, NameAdjust = NameAdjust.Prefix0, SilenceIssues = false, StorageType = ContainerStorageType.None
            /// </summary>
            public AnnotatedItemAttributeBase() 
                : base() 
            {
                Name = null; 
                NameAdjust = NameAdjust.Prefix0;
                SilenceIssues = false;
                StorageType = ContainerStorageType.None;
            }

            /// <summary>
            /// This property allows the attribute definition to set the item Name that should be used in the type definition for this item.  
            /// Use null to force use the field or property MemberInfo.Name as the derived name.
            /// <para/>Defaults to null.
            /// </summary>
            public virtual string Name { get; set; }

            /// <summary>
            /// This property is used to guide the generation of the derived name by choosing which strategy is to be used.
            /// <para/>Defaults to Prefix0.  Other choices are None, Format, Prefix1, Prefix2, or Prefix3
            /// </summary>
            public virtual NameAdjust NameAdjust { get; set; }

            /// <summary>
            /// When an item is marked to SilenceIssues, no issue messages will be emitted if the value cannot be accessed.  Value messages will still be emitted.
            /// </summary>
            public bool SilenceIssues { get; set; }

            /// <summary>
            /// When this property is set to be any value other than None (its default), the value marshalling will attempt to cast the member value to/from this cotnainer type when setting/getting container contents.
            /// </summary>
            public ContainerStorageType StorageType { get; set; }

            /// <summary>
            /// Used to control the access for this item.  Combine this with UseGetter and UseSetter extension methods.
            /// Default value is ItemAccess.Normal (selects both UseSetterIfPresent and UseGetterIfPresent)
            /// </summary>
            public ItemAccess ItemAccess { get { return _itemAccess; } set { _itemAccess = value; } }
            private ItemAccess _itemAccess = ItemAccess.Normal;

            /// <summary>
            /// Generates a derived name from the given memberInfo's Name, the Name property, the NameAdjust property and the given paramsStrArray contents
            /// </summary>
            public string GenerateFullName(MemberInfo memberInfo, params string[] paramsStrArray)
            {
                return GenerateFullName(memberInfo.Name, paramsStrArray);
            }

            /// <summary>
            /// Generates a derived name from the given memberName, the Name property, the NameAdjust property and the given paramsStrArray contents
            /// </summary>
            public string GenerateFullName(string memberName, params string[] paramsStrArray)
            {
                return NameAdjust.GenerateFullName(Name, memberName, paramsStrArray);
            }

            /// <summary>
            /// gives C# client get only access to the NamedValueSet that contains some of the values that are selectable here.  
            /// The getter will generate an NVS to contain the the merged contents of the AdditionalKeywords and the results from calling the GetDerivedTypeMetaDataToMerge contents.
            /// </summary>
            public INamedValueSet MetaData
            {
                get
                {
                    if (metaData == null)
                        metaData = GetMergedMetaData();

                    return metaData;
                }
            }
            private INamedValueSet metaData = null;

            /// <summary>
            /// Derived types may override this method implementation to allow them merge the returned meta-data into the per key meta-data that will be used with the key access objects generated here from this attribute.
            /// <para/>Please note that this method should always return an INVS instance that has already been converted to be ReadOnly so that later layers where this is also done will not generate an excessive number of clones of the original one returned here.
            /// </summary>
            protected virtual INamedValueSet GetDerivedTypeMetaDataToMerge() { return null; }

            /// <summary>
            /// Allows the caller to specify a set of additional keywords to be included in the NamedValueSet that will be produced when using the MetaData property.
            /// </summary>
            public string[] AdditionalKeywords { get { return (additionalKeywords ?? EmptyArrayFactory<string>.Instance); } set { additionalKeywords = value; additionalKeywordsHasBeenSet = true; metaData = null; } }
            private string[] additionalKeywords = null;
            private bool additionalKeywordsHasBeenSet = false;

            /// <summary>
            /// This method combines and returns the attribute meta data obtained from GetDerivedTypeMetaDataToMerge with any AdditionalKeywords and with any given <paramref name="mergeWithMetaData"/>.
            /// </summary>
            public INamedValueSet GetMergedMetaData(INamedValueSet mergeWithMetaData = null, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate)
            {
                INamedValueSet derivedTypeMetaDataToMerge = GetDerivedTypeMetaDataToMerge();

                if (!additionalKeywordsHasBeenSet)
                {
                    if (!derivedTypeMetaDataToMerge.IsNullOrEmpty())
                        return derivedTypeMetaDataToMerge.MergeWith(mergeWithMetaData, mergeBehavior: mergeBehavior);
                    else
                        return mergeWithMetaData ?? NamedValueSet.Empty;
                }
                else
                {
                    NamedValueSet nvs = new NamedValueSet();

                    if (additionalKeywordsHasBeenSet)
                        nvs.AddRange(AdditionalKeywords.Select(keyword => new NamedValue(keyword) { IsReadOnly = true }));

                    if (!derivedTypeMetaDataToMerge.IsNullOrEmpty())
                        nvs = nvs.MergeWith(derivedTypeMetaDataToMerge, mergeBehavior: NamedValueMergeBehavior.AddAndUpdate);

                    if (mergeWithMetaData != null)
                        nvs = nvs.MergeWith(mergeWithMetaData, mergeBehavior: mergeBehavior);

                    return nvs.ConvertToReadOnly();
                }
            }
        }

        /// <summary>
        /// Enum is used with <see cref="MosaicLib.Modular.Reflection.Attributes.IAnnotatedItemAttribute"/> 
        /// to define how an Annotated Item's full name is adjusted/generated from its derived Name (memberInfo.Name combined with Name property) and the
        /// array of params strings that are generally given to the corresonding Setup type method that actually generates the full names.  
        /// <para/>Supported values: None, Format, FormatWithMemberName, Prefix0, Prefix1, Prefix2, Prefix3
        /// <para/>A, B, C, D are also supported for backwards compatibility - these map to Prefix0 through Prefix3 respectively.
        /// </summary>
        public enum NameAdjust : int
        {
            /// <summary>The item's Name property (if non-null) or the items MemberInfo.Name will be used without further modification.</summary>
            None = 0,

            /// <summary>The params string[0] is used as a prefix (if it is present and non-empty).  This is the default value.</summary>
            Prefix0 = 1,
            /// <summary>The params string[1] is used as a prefix (if it is present and non-empty)</summary>
            Prefix1 = 2,
            /// <summary>The params string[2] is used as a prefix (if it is present and non-empty)</summary>
            Prefix2 = 3,
            /// <summary>The params string[3] is used as a prefix (if it is present and non-empty)</summary>
            Prefix3 = 4,

            /// <summary>For backwards compatibility.  Identical to Prefix0 (1)</summary>
            A = Prefix0,
            /// <summary>For backwards compatibility.  Identical to Prefix1 (2)</summary>
            B = Prefix1,
            /// <summary>For backwards compatibility.  Identical to Prefix2 (3)</summary>
            C = Prefix2,
            /// <summary>For backwards compatibility.  Identical to Prefix3 (4)</summary>
            D = Prefix3,

            /// <summary>
            /// Uses the Name property as the format string combined with the corresonding params string array values
            /// to support more arbitrary mechanisms for generating derived names.  
            /// This version uses MosaicLib.Utils.Fcns.CheckedFormat and as such will produce error messages if the referenced {} arguments do not match the actual set of params strings
            /// that are passed to the method.
            /// (5)
            /// </summary>
            Format = 5,

            /// <summary>
            /// Uses the Name property as the format string combined with the corresonding params string array values, prefixed with the memberInfo.Name value,
            /// to support more arbitrary mechanisms for generating derived names.  As such format field {0} becomes the memberInfo.Name while {1} ... {n} given params string array
            /// values 0 .. n-1.
            /// This version uses MosaicLib.Utils.Fcns.CheckedFormat and as such will produce error messages if the referenced {} arguments do not match the actual set of params strings
            /// that are passed to the method.
            /// (6)
            /// </summary>
            FormatWithMemberName = 6,
        };

        public static partial class ExtensionMethods
        {
            /// <summary>
            /// Generates a derived Name from the given nameAdjust value, itemName, memberName, and the given paramsStrArray contents to be used as potential Prefix values or string format parameters.
            /// <para/>This method generally has the following behavior:
            /// <para/>inferredName = itemName ?? memberName ?? String.Empty
            /// <para/>NameAdjust.None: return inferredName
            /// <para/>NameAdjust.Format: return itemName.CheckedFormat(paramsStrArray)
            /// <para/>NameAdjust.FormatWithMemberName: return itemName.CheckedFormat(memberName, paramStrArray)
            /// <para/>NameAdjust.Prefix0: return paramsStrArray[0] + inferredName
            /// <para/>NameAdjust.Prefix1: return paramsStrArray[1] + inferredName
            /// <para/>NameAdjust.Prefix2: return paramsStrArray[2] + inferredName
            /// <para/>NameAdjust.Prefix3: return paramsStrArray[3] + inferredName
            /// </summary>
            public static string GenerateFullName(this NameAdjust nameAdjust, string itemName, string memberName, params string[] paramsStrArray)
            {
                string inferredName = itemName ?? memberName ?? string.Empty;

                switch (nameAdjust)
                {
                    case NameAdjust.None: return inferredName;
                    case NameAdjust.Format: return (itemName ?? string.Empty).CheckedFormat(paramsStrArray);
                    case Attributes.NameAdjust.FormatWithMemberName:
                        {
                            List<string> paramsStrList = new List<string>();
                            paramsStrList.Add(memberName ?? string.Empty);
                            paramsStrList.AddRange(paramsStrArray);
                            return (itemName ?? string.Empty).CheckedFormat(paramsStrList.ToArray());
                        }
                    case NameAdjust.Prefix0: return paramsStrArray.SafeAccess(0, string.Empty) + inferredName;
                    case NameAdjust.Prefix1: return paramsStrArray.SafeAccess(1, string.Empty) + inferredName;
                    case NameAdjust.Prefix2: return paramsStrArray.SafeAccess(2, string.Empty) + inferredName;
                    case NameAdjust.Prefix3: return paramsStrArray.SafeAccess(3, string.Empty) + inferredName;
                    default: return string.Empty;
                }
            }
        }

        /// <summary>
        /// Defines which public properties and/or fields are included in the ValueSet representation 
        /// As a flag enum, user may combine enum values using the or operator "|"
        /// <para/>None (0x00), IncludeExplicitPublicItems (0x01), IncludeAllPublicProperties (0x02), IncludeAllPublicFields (0x04), IncludeExplicitItems (0x08), IncludeInheritedItems(0x10), UseStrictAttributeTypeChecking(0x20)
        /// </summary>
        [Flags]
        public enum ItemSelection
        {
            /// <summary>Set is empty (Default value): 0x00</summary>
            None = 0x00,
            /// <summary>Set items include all public fields or properties that carry an Attributes.Item attribute.  0x01</summary>
            IncludeExplicitPublicItems = 0x01,
            /// <summary>ValueSet items include all public properties.  0x02</summary>
            IncludeAllPublicProperties = 0x02,
            /// <summary>ValueSet items include all public fields.  0x04</summary>
            IncludeAllPublicFields = 0x04,
            /// <summary>items include all fields or properties that carry an Attrirbute.Item attribute.  0x08</summary>
            IncludeExplicitItems = 0x08,
            /// <summary>
            /// This value must be combined with other values.  
            /// When included it selects that inherited properties and fields should be considered in addition to declared ones.
            /// When not included (the default), only declared properties and fields are considered.
            /// 0x10
            /// </summary>
            IncludeInheritedItems = 0x10,
            /// <summary>
            /// When set, only items that are annotated with the specific given type (by reference equality) will be used.  Otherwise the first attribute of the indicated type will be used.
            /// </summary>
            UseStrictAttributeTypeChecking = 0x20,
        }

        /// <summary>
        /// Defines the access type that is supported by the associated item(s)
        /// <para/>None (0x00), UseGetterIfPresent (0x01), UseSetterIfPresent (0x02), Normal (0x03), GetOnly (0x01), SetOnly (0x02)
        /// <para/>None (0x00), Read (0x01), Write(0x02), ReadWrite (Read | Write)
        /// </summary>
        [Flags]
        public enum ItemAccess : int
        {
            /// <summary>item is not accessible: 0x00</summary>
            None = 0x00,

            /// <summary>allow adapter to attempt to get from this item: 0x01</summary>
            UseGetterIfPresent = 0x01,

            /// <summary>allow adapter to attempt to get from this item: 0x02</summary>
            UseSetterIfPresent = 0x02,

            /// <summary>adapter can use this item normally (UseGetterIfPresent | UseSetterIfPresent) == 0x03</summary>
            Normal = (UseGetterIfPresent | UseSetterIfPresent),

            /// <summary>adapter can get from this item but cannot set it (UseGetterIfPresent) == 0x01</summary>
            GetOnly = UseGetterIfPresent,

            /// <summary>adapter can set this item but cannot get it (UseSetterIfPresent) == 0x02</summary>
            SetOnly = UseSetterIfPresent,

            /// <summary>item can be read/serialized: 0x01</summary>
            Read = 0x01,

            /// <summary>ValueSet items can be written/deserialized: 0x02</summary>
            Write = 0x02,

            /// <summary>ValueSet items can be read/serialized and can be written/deserialized: (Read | Write) == 0x03</summary>
            ReadWrite = (Read | Write),
        }

        public static partial class ExtensionMethods
        {
            /// <summary>
            /// Returns true if ItemAccess.UseGetterIfPresent is in the given itemAccess value
            /// </summary>
            public static bool UseGetter(this ItemAccess itemAccess)
            {
                return itemAccess.IsSet(ItemAccess.UseGetterIfPresent);
            }

            /// <summary>
            /// Returns true if ItemAccess.UseSetterIfPresent is in the given itemAccess value
            /// </summary>
            public static bool UseSetter(this ItemAccess itemAccess)
            {
                return itemAccess.IsSet(ItemAccess.UseSetterIfPresent);
            }
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

            /// <summary>
            /// Gives the annotated attribute as a System.Attribute
            /// <para/>may be null if property or field does not carry any Item Attribute... or if the attribute type is not derived from System.Attribute
            /// </summary>
            public System.Attribute ItemAttribute { get; set; }

            /// <summary>
            /// Gives the annotated attribute as an IAnnotatedItemAttribute.
            /// <para/>may be null if property or field does not carry any suitable ItemAttribute, or if the actual attribute type does not implement IAnnotatedItemAttribute
            /// </summary>
            public IAnnotatedItemAttribute IAnnotatedItemAttribute { get; set; }

            /// <summary>Returns true if the IAnnotatedItemAttribute is non-null and its Name property is non-null.</summary>
            public bool HasCustomAnnotatedName { get { return (IAnnotatedItemAttribute != null && IAnnotatedItemAttribute.Name != null); } }

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

            /// <summary>
            /// getter Returns the PropertyInfo or FieldInfo as a MemberInfo (for access to common sub-properties such as Name).
            /// <para/>setter updates PropertInfo, FieldInfo and ItemType based on the given MemberInfo value.
            /// </summary>
            public MemberInfo MemberInfo 
            { 
                get { return (PropertyInfo ?? FieldInfo ?? _memberInfo); } 
                set 
                { 
                    _memberInfo = value; 
                    PropertyInfo = value as PropertyInfo; 
                    FieldInfo = value as FieldInfo;
                    ItemType = IsProperty ? PropertyInfo.PropertyType : (IsField ? FieldInfo.FieldType : null);
                } 
            }
            private MemberInfo _memberInfo = null;

            /// <summary>True if the item can get the member value and the item's IAnnotatedItemAttribute.ItemAccess permits use of the getter.  True for all fields and for properties that CanRead.</summary>
            public bool CanGetValue 
            { 
                get 
                {
                    ItemAccess itemAccess = (IAnnotatedItemAttribute != null ? IAnnotatedItemAttribute.ItemAccess : ItemAccess.Normal);

                    return (itemAccess.UseGetter() && (IsField || (IsProperty && PropertyInfo.CanRead))); 
                } 
            }

            /// <summary>True if the item can set the member value and the item's IAnnotatedItemAttribute.ItemAccess permits use of the setter.  True for all fields and for properties that CanWrite.</summary>
            public bool CanSetValue 
            { 
                get 
                {
                    ItemAccess itemAccess = (IAnnotatedItemAttribute != null ? IAnnotatedItemAttribute.ItemAccess : ItemAccess.Normal);

                    return (itemAccess.UseSetter() && (IsField || (IsProperty && PropertyInfo.CanWrite))); 
                } 
            }

            /// <summary>Generate string version of this Item Info for debugging and logging purposes.</summary>
            public override string ToString()
            {
                string infoStr;

                if (IsProperty)
                    infoStr = Fcns.CheckedFormat("Property {0}{1}{2}", PropertyInfo.Name, (CanGetValue ? ",Get" : ",NoGet"), (CanSetValue ? ",Set" : ",NoSet"));
                else if (IsField)
                    infoStr = Fcns.CheckedFormat("Field {0}", FieldInfo.Name);
                else if (MemberInfo != null)
                    infoStr = Fcns.CheckedFormat("UnsupportedMemberType {0}:{1}", MemberInfo.Name, MemberInfo.MemberType);
                else
                    infoStr = Fcns.CheckedFormat("NoMemberInfoGiven");

                if (HasCustomAnnotatedName)
                    return infoStr;
                else
                    return "{0} AnnotatedName:{1}".CheckedFormat(infoStr, IAnnotatedItemAttribute.Name);
            }

            /// <summary>If the item IsUsingAnnotatedName then it returs the IAnnotatedItemAttribute's Name, otherwise it returns the MemberInfo's Name</summary>
            public string DerivedName { get { return (HasCustomAnnotatedName ? IAnnotatedItemAttribute.Name : MemberInfo.Name); } }

            /// <summary>
            /// Generates a derived name from the given memberInfo's Name, the Name property, the NameAdjust property and the given paramsStrArray contents
            /// </summary>
            public string GenerateFullName(params string[] paramsStrArray)
            {
                if (IAnnotatedItemAttribute != null)
                    return IAnnotatedItemAttribute.GenerateFullName(MemberInfo, paramsStrArray);
                else
                    return MemberInfo.Name;
            }
        }

        /// <summary>
        /// Container class for information that is extracted from an class using the AnnotatedClassItemAccessListExtractor class
        /// </summary>
        public class ItemInfo<TItemAttribute>
            : ItemInfo
            where TItemAttribute : IAnnotatedItemAttribute
        {
            private TItemAttribute itemAttribute;

            /// <summary>Replaces ItemInfo base Attribute with derived type specific version.</summary>
            /// <remarks>may be null if property or field does not carry any Item Attribute...</remarks>
            public new TItemAttribute ItemAttribute 
            { 
                get { return itemAttribute; } 
                set 
                { 
                    itemAttribute = value; 
                    base.ItemAttribute = value as System.Attribute;
                    base.IAnnotatedItemAttribute = value as IAnnotatedItemAttribute;
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
            #region GenerateGetMemberToVCFunc, GenerateSetMemberFromVCAction, GetMemberAsVCFunctionDelegate, SetMemberFromVCActionDelegate

            /// <summary>
            /// Delegate signature for methods generated here that are used to Get from a previously indicated member and return its value in a ValueContainer.  
            /// The provided issue emitter will be used to record any issues and the provided value update emitter will be used to report assigned values.
            /// </summary>
            public delegate ValueContainer GetMemberAsVCFunctionDelegate<TAnnotatedClass>(TAnnotatedClass instance, Logging.IMesgEmitter updateIssueEmitter, Logging.IMesgEmitter valueUpdateEmitter, bool rethrow);

            /// <summary>
            /// Delegate signature for methods generated here that are used to Set a preivsouly indicated member from the value contained in the given ValueContainer (if possible).
            /// The provided issue emitter will be used to record any issues and the provided value update emitter will be used to report assigned values.
            /// </summary>
            public delegate void SetMemberFromVCActionDelegate<TAnnotatedClass>(TAnnotatedClass instance, ValueContainer vc, Logging.IMesgEmitter updateIssueEmitter, Logging.IMesgEmitter valueUpdateEmitter, bool rethrow);

            /// <summary>
            /// Extension method that will generate a GetMemberAsVCFunctionDelegate method for the member specified by this itemInfo.  
            /// Supports bool, sbyte, short, int, long, byte, ushort, uint, ulong, float, double, nullables of these, string, object, string [], ValueContainer, IList{string}, IList{ValueContainer} and enumeration types.
            /// This method will return null if the given itemInfo indicates that it is not gettable (CanGetValue property givens false)
            /// </summary>
            public static GetMemberAsVCFunctionDelegate<TAnnotatedClass> GenerateGetMemberToVCFunc<TAnnotatedClass>(this ItemInfo itemInfo)
            {
                if (!itemInfo.CanGetValue)
                    return null;

                IAnnotatedItemAttribute itemAttribute = itemInfo.IAnnotatedItemAttribute;

                ValueContainer.DecodedTypeInfo dti = ValueContainer.GetDecodedTypeInfo(itemInfo.ItemType);
                ContainerStorageType useStorageType = dti.cst;
                bool isNullable = dti.isNullable;
                Func<TAnnotatedClass, ValueContainer> vcGetter = null;

                if (itemAttribute != null && !itemAttribute.StorageType.IsNone())
                    useStorageType = itemAttribute.StorageType;

                bool silenceIssues = (itemAttribute != null ? itemAttribute.SilenceIssues : false);
                bool canUseQuickSetValue = (useStorageType == dti.cst);

                if (itemInfo.ItemType == typeof(bool))
                {
                    Func<TAnnotatedClass, bool> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, bool>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<bool>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<bool>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(sbyte))
                {
                    Func<TAnnotatedClass, sbyte> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, sbyte>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<sbyte>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<sbyte>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(short))
                {
                    Func<TAnnotatedClass, short> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, short>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<short>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<short>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(int))
                {
                    Func<TAnnotatedClass, int> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, int>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<int>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<int>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(long))
                {
                    Func<TAnnotatedClass, long> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, long>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<long>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<long>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(byte))
                {
                    Func<TAnnotatedClass, byte> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, byte>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<byte>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<byte>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(ushort))
                {
                    Func<TAnnotatedClass, ushort> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, ushort>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<ushort>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<ushort>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(uint))
                {
                    Func<TAnnotatedClass, uint> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, uint>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<uint>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<uint>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(ulong))
                {
                    Func<TAnnotatedClass, ulong> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, ulong>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<ulong>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<ulong>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(float))
                {
                    Func<TAnnotatedClass, float> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, float>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<float>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<float>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(double))
                {
                    Func<TAnnotatedClass, double> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, double>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<double>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<double>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(bool?))
                {
                    Func<TAnnotatedClass, bool?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, bool?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<bool?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<bool?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(sbyte?))
                {
                    Func<TAnnotatedClass, sbyte?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, sbyte?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<sbyte?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<sbyte?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(short?))
                {
                    Func<TAnnotatedClass, short?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, short?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<short?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<short?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(int?))
                {
                    Func<TAnnotatedClass, int?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, int?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<int?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<int?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(long?))
                {
                    Func<TAnnotatedClass, long?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, long?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<long?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<long?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(byte?))
                {
                    Func<TAnnotatedClass, byte?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, byte?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<byte?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<byte?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(ushort?))
                {
                    Func<TAnnotatedClass, ushort?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, ushort?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<ushort?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<ushort?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(uint?))
                {
                    Func<TAnnotatedClass, uint?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, uint?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<uint?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<uint?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(ulong?))
                {
                    Func<TAnnotatedClass, ulong?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, ulong?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<ulong?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<ulong?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(float?))
                {
                    Func<TAnnotatedClass, float?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, float?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<float?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<float?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(double?))
                {
                    Func<TAnnotatedClass, double?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, double?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<double?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<double?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(string))
                {
                    Func<TAnnotatedClass, string> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, string>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<string>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<string>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(object))
                {
                    Func<TAnnotatedClass, object> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, object>(itemInfo);
                    vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<object>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(string[]))
                {
                    Func<TAnnotatedClass, string[]> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, string[]>(itemInfo);
                    vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<string[]>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(ValueContainer[]))
                {
                    Func<TAnnotatedClass, ValueContainer[]> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, ValueContainer[]>(itemInfo);
                    vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<ValueContainer[]>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (typeof(IList<string>).IsAssignableFrom(itemInfo.ItemType))
                {
                    Func<TAnnotatedClass, IList<string>> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, IList<string>>(itemInfo);
                    vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<IList<string>>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (typeof(IList<ValueContainer>).IsAssignableFrom(itemInfo.ItemType))
                {
                    Func<TAnnotatedClass, IList<ValueContainer>> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, IList<ValueContainer>>(itemInfo);
                    vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<IList<ValueContainer>>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(ValueContainer))
                {
                    Func<TAnnotatedClass, ValueContainer> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, ValueContainer>(itemInfo);
                    vcGetter = (annotatedInstance) => { return (ValueContainer)pfGetter(annotatedInstance); };
                }
                else if (itemInfo.ItemType == typeof(Logging.LogGate))
                {
                    Func<TAnnotatedClass, Logging.LogGate> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, Logging.LogGate>(itemInfo);
                    vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<Logging.LogGate>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(Logging.LogGate ?))
                {
                    Func<TAnnotatedClass, Logging.LogGate ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, Logging.LogGate ?>(itemInfo);
                    vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<Logging.LogGate ?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(TimeSpan))
                {
                    Func<TAnnotatedClass, TimeSpan> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, TimeSpan>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<TimeSpan>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<TimeSpan>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(TimeSpan ?))
                {
                    Func<TAnnotatedClass, TimeSpan ?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, TimeSpan ?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<TimeSpan?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<TimeSpan?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(DateTime))
                {
                    Func<TAnnotatedClass, DateTime> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, DateTime>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<DateTime>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<DateTime>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (itemInfo.ItemType == typeof(DateTime?))
                {
                    Func<TAnnotatedClass, DateTime?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, DateTime?>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<DateTime?>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<DateTime?>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (typeof(INamedValueSet).IsAssignableFrom(itemInfo.ItemType))
                {
                    Func<TAnnotatedClass, INamedValueSet> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, INamedValueSet>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<INamedValueSet>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<INamedValueSet>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else if (typeof(INamedValue).IsAssignableFrom(itemInfo.ItemType))
                {
                    Func<TAnnotatedClass, INamedValue> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TAnnotatedClass, INamedValue>(itemInfo);
                    if (canUseQuickSetValue)
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<INamedValue>(pfGetter(annotatedInstance)); };
                    else
                        vcGetter = (annotatedInstance) => { return default(ValueContainer).SetValue<INamedValue>(pfGetter(annotatedInstance), useStorageType, isNullable); };
                }
                else
                {
                    // cover other types using ValueContainer's internal construction time object recognizer: enumeration, unrecognized, ...
                    // NOTE: that useStorageType and isNullable are ignored here.
                    vcGetter = (annotatedInstance) =>
                    {
                        object o = itemInfo.IsProperty ? itemInfo.PropertyInfo.GetValue(annotatedInstance, emptyObjectArray) : (itemInfo.IsField ? itemInfo.FieldInfo.GetValue(annotatedInstance) : null);

                        return default(ValueContainer).SetValue<object>(o, useStorageType, isNullable);
                    };
                }

                GetMemberAsVCFunctionDelegate<TAnnotatedClass> memberToVCDelegate 
                    = (TAnnotatedClass annotatedInstance, Logging.IMesgEmitter updateIssueEmitter, Logging.IMesgEmitter valueUpdateEmitter, bool rethrow) => 
                {
                    try
                    {
                        ValueContainer vc = vcGetter(annotatedInstance);

                        if (valueUpdateEmitter != null && valueUpdateEmitter.IsEnabled)
                            valueUpdateEmitter.Emit("Got value '{0}' from DerivedName:'{1}' [type:'{2}']", vc, itemInfo.DerivedName, itemInfo.ItemType);

                        return vc;
                    }
                    catch (System.Exception ex)
                    {
                        GlobalExceptionCount++;
                        GlobalLastException = ex;

                        if (!silenceIssues && updateIssueEmitter != null && updateIssueEmitter.IsEnabled)
                            updateIssueEmitter.Emit("Unable to get value from DerivedName:'{0}' [type:'{1}']: {2}", itemInfo.DerivedName, itemInfo.ItemType, ex);

                        if (rethrow)
                            throw;

                        return ValueContainer.Empty;
                    }
                };

                return memberToVCDelegate;
            }

            /// <summary>
            /// Extension method that will generate a SetMemberFromVCFunctionDelegate method for the member specified by this itemInfo.  
            /// Supports bool, sbyte, short, int, long, byte, ushort, uint, ulong, float, double, nullables of these, string, object, string [], ValueContainer, IList{string}, IList{ValueContainer} and enumeration types.
            /// This method will return null if the given itemInfo indicates that it is not settable (CanSetValue property givens false)
            /// </summary>
            public static SetMemberFromVCActionDelegate<TAnnotatedClass> GenerateSetMemberFromVCAction<TAnnotatedClass>(this ItemInfo itemInfo, bool forceRethrowFlag = true)
            {
                if (!itemInfo.CanSetValue)
                    return null;

                IAnnotatedItemAttribute itemAttribute = itemInfo.IAnnotatedItemAttribute;                
                Action<TAnnotatedClass, ValueContainer, bool> vcSetter = null;

                bool silenceIssues = (itemAttribute != null ? itemAttribute.SilenceIssues : false);

                Type nullableBaseType = itemInfo.ItemType.GetNullableBaseType();
                bool isNullable = (nullableBaseType != null);

                if (itemInfo.ItemType == typeof(bool))
                {
                    Action<TAnnotatedClass, bool> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, bool>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<bool>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(sbyte))
                {
                    Action<TAnnotatedClass, sbyte> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, sbyte>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<sbyte>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(short))
                {
                    Action<TAnnotatedClass, short> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, short>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<short>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(int))
                {
                    Action<TAnnotatedClass, int> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, int>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<int>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(long))
                {
                    Action<TAnnotatedClass, long> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, long>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<long>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(byte))
                {
                    Action<TAnnotatedClass, byte> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, byte>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<byte>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(ushort))
                {
                    Action<TAnnotatedClass, ushort> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, ushort>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<ushort>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(uint))
                {
                    Action<TAnnotatedClass, uint> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, uint>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<uint>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(ulong))
                {
                    Action<TAnnotatedClass, ulong> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, ulong>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<ulong>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(float))
                {
                    Action<TAnnotatedClass, float> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, float>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<float>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(double))
                {
                    Action<TAnnotatedClass, double> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, double>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<double>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(bool?))
                {
                    Action<TAnnotatedClass, bool?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, bool?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<bool?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(sbyte?))
                {
                    Action<TAnnotatedClass, sbyte?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, sbyte?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<sbyte?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(short?))
                {
                    Action<TAnnotatedClass, short?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, short?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<short?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(int?))
                {
                    Action<TAnnotatedClass, int?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, int?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<int?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(long?))
                {
                    Action<TAnnotatedClass, long?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, long?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<long?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(byte?))
                {
                    Action<TAnnotatedClass, byte?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, byte?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<byte?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(ushort?))
                {
                    Action<TAnnotatedClass, ushort?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, ushort?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<ushort?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(uint?))
                {
                    Action<TAnnotatedClass, uint?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, uint?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<uint?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(ulong?))
                {
                    Action<TAnnotatedClass, ulong?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, ulong?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<ulong?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(float?))
                {
                    Action<TAnnotatedClass, float?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, float?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<float?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(double?))
                {
                    Action<TAnnotatedClass, double?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, double?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<double?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(string))
                {
                    Action<TAnnotatedClass, string> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, string>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<string>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(object))
                {
                    Action<TAnnotatedClass, object> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, object>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.ValueAsObject); };
                }
                else if (itemInfo.ItemType == typeof(string[]))
                {
                    Action<TAnnotatedClass, string[]> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, string[]>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<string[]>(decodedValueType: ContainerStorageType.IListOfString, isNullable: false, allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(ValueContainer[]))
                {
                    Action<TAnnotatedClass, ValueContainer[]> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, ValueContainer[]>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<ValueContainer[]>(decodedValueType: ContainerStorageType.IListOfVC, isNullable: false, allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (typeof(IList<string>).IsAssignableFrom(itemInfo.ItemType))
                {
                    Action<TAnnotatedClass, IList<string>> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, IList<string>>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<IList<string>>(decodedValueType: ContainerStorageType.IListOfString, isNullable: false, allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (typeof(IList<ValueContainer>).IsAssignableFrom(itemInfo.ItemType))
                {
                    Action<TAnnotatedClass, IList<ValueContainer>> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, IList<ValueContainer>>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<IList<ValueContainer>>(decodedValueType: ContainerStorageType.IListOfVC, isNullable: false, allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(ValueContainer))
                {
                    Action<TAnnotatedClass, ValueContainer> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, ValueContainer>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc); };
                }
                else if (itemInfo.ItemType == typeof(Logging.LogGate))
                {
                    Action<TAnnotatedClass, Logging.LogGate> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, Logging.LogGate>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<Logging.LogGate>(decodedValueType: ContainerStorageType.Custom, isNullable: false, allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(Logging.LogGate ?))
                {
                    Action<TAnnotatedClass, Logging.LogGate ?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, Logging.LogGate ?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<Logging.LogGate?>(decodedValueType: ContainerStorageType.Custom, isNullable: true, allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(TimeSpan))
                {
                    Action<TAnnotatedClass, TimeSpan> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, TimeSpan>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<TimeSpan>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(TimeSpan?))
                {
                    Action<TAnnotatedClass, TimeSpan?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, TimeSpan?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<TimeSpan?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(DateTime))
                {
                    Action<TAnnotatedClass, DateTime> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, DateTime>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<DateTime>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType == typeof(DateTime?))
                {
                    Action<TAnnotatedClass, DateTime?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, DateTime?>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<DateTime?>(allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (typeof(INamedValueSet).IsAssignableFrom(itemInfo.ItemType))
                {
                    Action<TAnnotatedClass, INamedValueSet> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, INamedValueSet>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<INamedValueSet>(decodedValueType: ContainerStorageType.INamedValueSet, isNullable: false, allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (typeof(INamedValue).IsAssignableFrom(itemInfo.ItemType))
                {
                    Action<TAnnotatedClass, INamedValue> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TAnnotatedClass, INamedValue>(itemInfo);
                    vcSetter = (annotatedInstance, vc, rethrow) => { pfSetter(annotatedInstance, vc.GetValue<INamedValue>(decodedValueType: ContainerStorageType.INamedValue, isNullable: false, allowTypeChangeAttempt: true, rethrow: rethrow || forceRethrowFlag)); };
                }
                else if (itemInfo.ItemType.IsEnum || (nullableBaseType != null && nullableBaseType.IsEnum))
                {
                    Type enumType = nullableBaseType ?? itemInfo.ItemType;

                    vcSetter = (annotatedInstance, vc, rethrow) =>
                    {
                        rethrow |= forceRethrowFlag;

                        object value = null;

                        try
                        {
                            if (vc.IsNullOrEmpty)
                            {
                                if (!isNullable && rethrow)
                                    throw new System.InvalidCastException("Cannot cast {0} to be of type {1}".CheckedFormat(vc, itemInfo.ItemType));
                            }
                            else
                            {
                                switch (vc.cvt)
                                {
                                    case ContainerStorageType.I1: value = System.Enum.ToObject(enumType, vc.u.i8); break;
                                    case ContainerStorageType.I2: value = System.Enum.ToObject(enumType, vc.u.i16); break;
                                    case ContainerStorageType.I4: value = System.Enum.ToObject(enumType, vc.u.i32); break;
                                    case ContainerStorageType.I8: value = System.Enum.ToObject(enumType, vc.u.i64); break;
                                    case ContainerStorageType.U1: value = System.Enum.ToObject(enumType, vc.u.u8); break;
                                    case ContainerStorageType.U2: value = System.Enum.ToObject(enumType, vc.u.u16); break;
                                    case ContainerStorageType.U4: value = System.Enum.ToObject(enumType, vc.u.u32); break;
                                    case ContainerStorageType.U8: value = System.Enum.ToObject(enumType, vc.u.u64); break;
                                    default:
                                        value = System.Enum.Parse(enumType, vc.GetValue<string>(rethrow));
                                        break;
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            GlobalExceptionCount++;
                            GlobalLastException = ex;

                            if (rethrow)
                                throw;
                        }

                        if (value == null && !isNullable)
                            value = itemInfo.ItemType.CreateDefaultInstance();

                        if (itemInfo.IsProperty)
                            itemInfo.PropertyInfo.SetValue(annotatedInstance, value, emptyObjectArray);
                        else
                            itemInfo.FieldInfo.SetValue(annotatedInstance, value);
                    };
                }
                else
                {
                    // cover other types using ValueContainer's internal construction time object recognizer: unrecognized, ...

                    vcSetter = (annotatedInstance, vc, rethrow) =>
                    {
                        object o = System.Convert.ChangeType(vc.ValueAsObject, itemInfo.ItemType);

                        if (itemInfo.IsProperty)
                            itemInfo.PropertyInfo.SetValue(annotatedInstance, o, emptyObjectArray);
                        else
                            itemInfo.FieldInfo.SetValue(annotatedInstance, o);
                    };
                }

                SetMemberFromVCActionDelegate<TAnnotatedClass> memberFromVCDelegate 
                    = (TAnnotatedClass annotatedInstance, ValueContainer vc, Logging.IMesgEmitter updateIssueEmitter, Logging.IMesgEmitter valueUpdateEmitter, bool rethrow) =>
                {
                    try
                    {
                        vcSetter(annotatedInstance, vc, rethrow);

                        if (valueUpdateEmitter != null && valueUpdateEmitter.IsEnabled)
                            valueUpdateEmitter.Emit("Set value '{0}' to DerivedName:'{1}' [type:'{2}']", vc, itemInfo.DerivedName, itemInfo.ItemType);
                    }
                    catch (System.Exception ex)
                    {
                        GlobalExceptionCount++;
                        GlobalLastException = ex;

                        if (!silenceIssues && updateIssueEmitter != null && valueUpdateEmitter.IsEnabled)
                            updateIssueEmitter.Emit("Unable to set value '{0}' to DerivedName:'{1}' [type:'{2}']: {3}", vc, itemInfo.DerivedName, itemInfo.ItemType, ex);

                        if (rethrow)
                            throw;
                    }
                };

                return memberFromVCDelegate;
            }

            private static readonly object[] emptyObjectArray = EmptyArrayFactory<object>.Instance;

            #endregion

            #region GenerateGetter, GenerateSetter

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

            #endregion

            #region static global exception counts and latched exceptions

            /// <summary>
            /// Global static volatile pseudo count of the number of exceptions that have occurred within a Reflection.Attributes method call.
            /// <para/>Note: As this variable is not atomically incremented, this count may not exactly represent the total count of such exceptions if two such exceptions are generated at the exact same time on two differnt cpu cores.
            /// </summary>
            public static volatile int GlobalExceptionCount = 0;

            /// <summary>
            /// Global static volatile field that gives the last exception that was generated and caught during a Reflection.Attributes method call.
            /// </summary>
            public static volatile System.Exception GlobalLastException = null;

            #endregion
        }

        /// <summary>
        /// Templatized helper class used to facilitate use of attributes derived from <see cref="IAnnotatedItemAttribute"/> including extracting them from 
        /// an annotated class and to help generate accessor functions for these annotated items.
        /// </summary>
        public static class AnnotatedClassItemAccessHelper<TItemAttribute> where TItemAttribute : IAnnotatedItemAttribute
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

                bool includeExplicitItems = ((itemSelection & ItemSelection.IncludeExplicitItems) != default(ItemSelection));
                bool includeExplicitPublicItems = ((itemSelection & (ItemSelection.IncludeExplicitPublicItems | ItemSelection.IncludeExplicitItems)) != default(ItemSelection));
                bool includeAllPublicProperties = ((itemSelection & ItemSelection.IncludeAllPublicProperties) != default(ItemSelection));
                bool includeAllPublicFields = ((itemSelection & ItemSelection.IncludeAllPublicFields) != default(ItemSelection));
                bool includeInheritedItems = ((itemSelection & ItemSelection.IncludeInheritedItems) != default(ItemSelection));
                bool useStrictAttributeTypeChecking = ((itemSelection & ItemSelection.UseStrictAttributeTypeChecking) != default(ItemSelection));

                List<ItemInfo<TItemAttribute>> itemAccessInfoList = new List<ItemInfo<TItemAttribute>>();

                BindingFlags bindingFlags = (BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
 
                if (!includeInheritedItems)
                    bindingFlags |= BindingFlags.DeclaredOnly;

                PropertyInfo[] piSet = annotatedClassType.GetProperties(bindingFlags);

                foreach (PropertyInfo pi in piSet)
                {
                    bool canRead = pi.CanRead;
                    bool canWrite = pi.CanWrite;
                    Type piType = pi.PropertyType;
                    MethodInfo getPMI = pi.GetGetMethod();
                    MethodInfo setPMI = pi.GetSetMethod();
                    bool isGetMethodAvailable = (getPMI != null);
                    bool isGetMethodPublic = (isGetMethodAvailable && getPMI.IsPublic);
                    bool isSetMethodAvailable = (setPMI != null);
                    bool isSetMethodPublic = (isSetMethodAvailable && setPMI.IsPublic);
                    bool isGetOrSetMethodPublic = (isGetMethodPublic || isSetMethodPublic);

                    object firstAttributeInArray = pi.GetCustomAttributes(ItemAttributeType, false).Where(attrib => attrib != null && (attrib.GetType() == ItemAttributeType || !useStrictAttributeTypeChecking)).FirstOrDefault();
                    TItemAttribute ia = (firstAttributeInArray is TItemAttribute) ? (TItemAttribute) firstAttributeInArray : default(TItemAttribute);

                    bool includeThisPublicProperty = (isGetOrSetMethodPublic && (includeAllPublicProperties || (ia != null && includeExplicitPublicItems)));
                    bool includeThisExplicitProperty = (!isGetOrSetMethodPublic && (ia != null && includeExplicitItems));
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

                    object firstAttributeInArray = fi.GetCustomAttributes(ItemAttributeType, false).Where(attrib => attrib != null && (attrib.GetType() == ItemAttributeType || !useStrictAttributeTypeChecking)).FirstOrDefault();
                    TItemAttribute ia = (firstAttributeInArray is TItemAttribute) ? (TItemAttribute)firstAttributeInArray : default(TItemAttribute);

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
