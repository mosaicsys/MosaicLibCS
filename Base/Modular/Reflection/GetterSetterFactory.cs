//-------------------------------------------------------------------
/*! @file GettersSettersFactory.cs
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
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Linq.Expressions;

namespace MosaicLib.Modular.Reflection
{
    /// <summary>
    /// This static class is a factory class that is used to generate Func/Action delegates that are used to get or set values for specific properties in a target object type
    /// </summary>
    public static class GetterSetterFactory
    {
        /// <summary>
        /// Creates a two argument getter delegate for a given object type (TObject), result type (TResult), and a selected Property (pi).  
        /// When called, this delegate will obtain and return the property's value as evaluated on the target object.
        /// </summary>
        /// <typeparam name="TObject">The actual type of the target object.  Must be an instance of the PropertyInfo.DeclaringType.</typeparam>
        /// <typeparam name="TResult">The property value type.  Must be the same as the PropertyInfo.PropertyType</typeparam>
        /// <param name="pi">The PropertyInfo for the property to be read.  Must not refer to a static property.</param>
        /// <exception cref="System.NotSupportedException">Exception thrown on any validation error as described above.</exception>
        public static Func<TObject, TResult> CreatePropertyGetterFunc<TObject, TResult>(PropertyInfo pi)
        {
            Type targetObjType = typeof(TObject);
            Type resultType = typeof(TResult);

            if (!targetObjType.Equals(pi.DeclaringType) && !targetObjType.IsSubclassOf(pi.DeclaringType))
                throw new System.NotSupportedException("Cannot create property getter function when given TObject is not derived from the given PropertyInfo instance's DeclaringType");
            if (!resultType.Equals(pi.PropertyType))
                throw new System.NotSupportedException("Cannot create property getter function when given TResult is not the same as the given PropertyInfo instance's PropertyType");
            if (pi.GetGetMethod().IsStatic)
                throw new System.NotSupportedException("Cannot create property setter action when given PropertyInfo's GetMethod is static");

            ParameterExpression targetObjParameter = Expression.Parameter(targetObjType, "targetObj");
            Expression propertyGetExpr = Expression.Property(targetObjParameter, pi);

            Func<TObject, TResult> func = Expression.Lambda<Func<TObject, TResult>>(propertyGetExpr, targetObjParameter).Compile();
            return func;
        }

        /// <summary>
        /// Creates a two argument setter delegate for a given object type (TObject), value type (TValue), and a selected Property (pi).
        /// When called, this delegate will assign the given value to the selected property in the given target object.
        /// </summary>
        /// <typeparam name="TObject">The actual type of the target object.  Must be an instance of the PropertyInfo.DeclaringType.</typeparam>
        /// <typeparam name="TValue">The property value type.  Must be the same as the PropertyInfo.PropertyType</typeparam>
        /// <param name="pi">The PropertyInfo for the property to be set.  Must not refer to a static property.</param>
        /// <exception cref="System.NotSupportedException">Exception thrown on any validation error as described above.</exception>
        public static Action<TObject, TValue> CreatePropertySetterAction<TObject, TValue>(PropertyInfo pi)
        {
            Type targetObjType = typeof(TObject);
            Type valueType = typeof(TValue);

            if (!targetObjType.Equals(pi.DeclaringType) && !targetObjType.IsSubclassOf(pi.DeclaringType))
                throw new System.NotSupportedException("Cannot create property setter function when given TObject is not derived from the given PropertyInfo instance's DeclaringType");
            if (targetObjType.IsValueType)
                throw new System.NotSupportedException("Cannot create property setter action when given TObject is a value type");
            if (!valueType.Equals(pi.PropertyType))
                throw new System.NotSupportedException("Cannot create property setter function when given TValue is not the same as the given PropertyInfo instance's PropertyType");
            if (pi.GetSetMethod().IsStatic)
                throw new System.NotSupportedException("Cannot create property setter action when given PropertyInfo's SetMethod is static");

            ParameterExpression targetObjParameter = Expression.Parameter(targetObjType, "targetObj");
            ParameterExpression valueParameter = Expression.Parameter(valueType, "value");
            MethodInfo mi = pi.GetSetMethod();
            MethodCallExpression propertySetExpr = Expression.Call(targetObjParameter, mi, valueParameter);

            Action<TObject, TValue> act = Expression.Lambda<Action<TObject, TValue>>(propertySetExpr, targetObjParameter, valueParameter).Compile();

            return act;
        }

        /// <summary>
        /// Creates a two argument getter delegate for a given object type (TObject), result type (TResult), and a selected Field (fi).  
        /// When called, this delegate will obtain and return the field's value as evaluated on the target object.
        /// </summary>
        /// <typeparam name="TObject">The actual type of the target object.  Must be an instance of the FieldInfo.DeclaringType.</typeparam>
        /// <typeparam name="TResult">The property value type.  Must be the same as the FieldInfo.FieldType</typeparam>
        /// <param name="fi">The FieldInfo for the property to be read.  Must not refer to a static field.</param>
        /// <exception cref="System.NotSupportedException">Exception thrown on any validation error as described above.</exception>
        public static Func<TObject, TResult> CreateFieldGetterExpression<TObject, TResult>(FieldInfo fi)
        {
            Type targetObjType = typeof(TObject);
            Type resultType = typeof(TResult);

            if (!targetObjType.Equals(fi.DeclaringType) && !targetObjType.IsSubclassOf(fi.DeclaringType))
                throw new System.NotSupportedException("Cannot create field getter function when given TObject is not derived from the given FieldInfo instance's DeclaringType");
            if (!resultType.Equals(fi.FieldType))
                throw new System.NotSupportedException("Cannot create field getter function when given TResult is not the same as the given FieldInfo instance's FieldType");
            if (fi.IsStatic)
                throw new System.NotSupportedException("Cannot create field getter action when given FieldInfo refers to a static field");

            ParameterExpression targetObjParameter = Expression.Parameter(targetObjType, "targetObj");
            Expression fieldReadExpr = Expression.Field(targetObjParameter, fi);

            Func<TObject, TResult> func = Expression.Lambda<Func<TObject, TResult>>(fieldReadExpr, targetObjParameter).Compile();
            return func;
        }

        /// <summary>
        /// Creates a two argument setter delegate for a given object type (TObject), value type (TValue), and a selected Field (fi).
        /// When called, this delegate will assign the given value to the selected field in the given target object.
        /// </summary>
        /// <typeparam name="TObject">The actual type of the target object.  Must be an instance of the FieldInfo.DeclaringType and must not be a value type.</typeparam>
        /// <typeparam name="TValue">The property value type.  Must be the same as the FieldInfo.FieldType</typeparam>
        /// <param name="fi">The FieldInfo for the field to be set.  Must not refer to a static field.</param>
        /// <exception cref="System.NotSupportedException">Exception thrown on any validation error as described above.</exception>
        public static Action<TObject, TValue> CreateFieldSetterAction<TObject, TValue>(FieldInfo fi)
        {
            Type targetObjType = typeof(TObject);
            Type valueType = typeof(TValue);

            if (!targetObjType.Equals(fi.DeclaringType) && !targetObjType.IsSubclassOf(fi.DeclaringType))
                throw new System.NotSupportedException("Cannot create field setter action when given TObject is not derived from the given FieldInfo instance's DeclaringType");
            if (targetObjType.IsValueType)
                throw new System.NotSupportedException("Cannot create field setter action when given TObject is a value type");
            if (!valueType.Equals(fi.FieldType))
                throw new System.NotSupportedException("Cannot create field setter action when given TValue is not the same as the given FieldInfo instance's FieldType");
            if (fi.IsStatic)
                throw new System.NotSupportedException("Cannot create field setter action when given FieldInfo refers to a static field");

            DynamicMethod setFieldMethod = new DynamicMethod("_setField_" + fi.Name, typeof(void), new[] { typeof(TObject), typeof(TValue) });
            ILGenerator ilGen = setFieldMethod.GetILGenerator();

            ilGen.Emit(OpCodes.Ldarg_0);        // get the target object (TObject type)
            ilGen.Emit(OpCodes.Ldarg_1);        // get the value (TValue)
            ilGen.Emit(OpCodes.Stfld, fi);      // store the TValue into the TObject.(fi.Name) field
            ilGen.Emit(OpCodes.Ret);            // done.

            Action<TObject, TValue> act = (Action<TObject, TValue>) setFieldMethod.CreateDelegate(typeof(Action<TObject, TValue>));
            return act;
        }
    }
}

//-------------------------------------------------------------------
