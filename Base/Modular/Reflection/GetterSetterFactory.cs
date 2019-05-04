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
        /// Creates a two argument getter delegate for a given object type {TObject}, result type {TResult}, and a selected Property <paramref name="pi"/>.  
        /// When called, this delegate will obtain and return the property's value as evaluated on the target object.
        /// </summary>
        /// <typeparam name="TObject">The actual type of the target object.  Must be an instance of the PropertyInfo.DeclaringType.</typeparam>
        /// <typeparam name="TResult">The property value type.  Must be the same as the PropertyInfo.PropertyType, or must be assignable from it.</typeparam>
        /// <param name="pi">The PropertyInfo for the property to be read.  Must not refer to a static property.</param>
        /// <param name="permitCastAttempt">When true this allows the generation of delegates that will code the attempt to perform the cast based type conversion even if the {TResult} is not known to be assignable from the given PropertyInfo.PropertyType</param>
        /// <exception cref="System.NotSupportedException">Exception thrown on any validation error as described above.</exception>
        public static Func<TObject, TResult> CreatePropertyGetterFunc<TObject, TResult>(PropertyInfo pi, bool permitCastAttempt = false)
        {
            Type targetObjType = typeof(TObject);
            Type resultType = typeof(TResult);

            if (!targetObjType.Equals(pi.DeclaringType) && !targetObjType.IsSubclassOf(pi.DeclaringType))
                throw new System.NotSupportedException("Cannot create property getter function when given TObject is not derived from the given PropertyInfo instance's DeclaringType");

            MethodInfo pgGetMethod = pi.GetGetMethod();
            if (pgGetMethod == null)
                throw new System.NotSupportedException("Cannot create property setter action when given PropertyInfo's GetMethod does not exist");

            if (pgGetMethod.IsStatic)
                throw new System.NotSupportedException("Cannot create property setter action when given PropertyInfo's GetMethod is static");

            if (resultType.Equals(pi.PropertyType))
            {
                ParameterExpression targetObjParameter = Expression.Parameter(targetObjType, "targetObj");
                Expression propertyGetExpr = Expression.Property(targetObjParameter, pi);
                var lambdaExpr = Expression.Lambda<Func<TObject, TResult>>(propertyGetExpr, targetObjParameter);

                Func<TObject, TResult> func = lambdaExpr.Compile();

                return func;
            }
            else if (resultType.IsAssignableFrom(pi.PropertyType) || permitCastAttempt)
            {
                ParameterExpression targetObjParameter = Expression.Parameter(targetObjType, "targetObj");
                Expression propertyGetExpr = Expression.Property(targetObjParameter, pi);
                Expression convertedValueExpr = Expression.Convert(propertyGetExpr, resultType);
                var lambdaExpr = Expression.Lambda<Func<TObject, TResult>>(convertedValueExpr, targetObjParameter);

                Func<TObject, TResult> func = lambdaExpr.Compile();

                return func;
            }
            else
            {
                throw new System.NotSupportedException("Cannot create property getter function when given TResult is not the same as the given PropertyInfo instance's PropertyType and the given TResult is not assignable from the PropertyType reference type.");
            }
        }

        /// <summary>
        /// Creates a two argument setter delegate for a given object type {TObject}, value type {TValue}, and a selected Property <paramref name="pi"/>.
        /// When called, this delegate will assign the given value to the selected property in the given target object.
        /// </summary>
        /// <typeparam name="TObject">The actual type of the target object.  Must be an instance of the PropertyInfo.DeclaringType.</typeparam>
        /// <typeparam name="TValue">The property value type.  Must be the same as the PropertyInfo.PropertyType, or must be assignable to it.</typeparam>
        /// <param name="pi">The PropertyInfo for the property to be set.  Must not refer to a static property.</param>
        /// <param name="permitCastAttempt">When true this allows the generation of delegates that will code the attempt to perform the cast based type conversion even if the PropertyInfo.PropertyType is not known to be assignable from the given {TValue} type</param>
        /// <exception cref="System.NotSupportedException">Exception thrown on any validation error as described above.</exception>
        public static Action<TObject, TValue> CreatePropertySetterAction<TObject, TValue>(PropertyInfo pi, bool permitCastAttempt = false)
        {
            Type targetObjType = typeof(TObject);
            Type valueType = typeof(TValue);

            if (!targetObjType.Equals(pi.DeclaringType) && !targetObjType.IsSubclassOf(pi.DeclaringType))
                throw new System.NotSupportedException("Cannot create property setter function when given TObject is not derived from the given PropertyInfo instance's DeclaringType");

            if (targetObjType.IsValueType)
                throw new System.NotSupportedException("Cannot create property setter action when given TObject is a value type");

            MethodInfo piSetMethod = pi.GetSetMethod();
            if (piSetMethod == null)
                throw new System.NotSupportedException("Cannot create property setter action when given PropertyInfo's SetMethod does not exist");

            if (piSetMethod.IsStatic)
                throw new System.NotSupportedException("Cannot create property setter action when given PropertyInfo's SetMethod is static");

            if (valueType.Equals(pi.PropertyType))
            {
                ParameterExpression targetObjParameter = Expression.Parameter(targetObjType, "targetObj");
                ParameterExpression inputParameter = Expression.Parameter(valueType, "input");
                MethodCallExpression propertySetExpr = Expression.Call(targetObjParameter, piSetMethod, inputParameter);
                var lambdaExpr = Expression.Lambda<Action<TObject, TValue>>(propertySetExpr, targetObjParameter, inputParameter);

                Action<TObject, TValue> act = lambdaExpr.Compile();

                return act;
            }
            else if (pi.PropertyType.IsAssignableFrom(valueType) || permitCastAttempt)
            {
                ParameterExpression targetObjParameter = Expression.Parameter(targetObjType, "targetObj");
                ParameterExpression inputParameter = Expression.Parameter(valueType, "input");
                Expression convertedValueExpr = Expression.Convert(inputParameter, pi.PropertyType);
                MethodCallExpression propertySetExpr = Expression.Call(targetObjParameter, piSetMethod, convertedValueExpr);
                var lambdaExpr = Expression.Lambda<Action<TObject, TValue>>(propertySetExpr, targetObjParameter, inputParameter);

                Action<TObject, TValue> act = lambdaExpr.Compile();

                return act;
            }
            else
            {
                throw new System.NotSupportedException("Cannot create property setter function when given TValue is not the same as the given PropertyInfo instance's PropertyType and the PropertyType is not assignable from the TValue reference type.");
            }
        }

        [Obsolete("Please switch to the use of the new equivilant CreateFieldGetterFunc method (2019-03-29)")]
        public static Func<TObject, TResult> CreateFieldGetterExpression<TObject, TResult>(FieldInfo fi)
        {
            return CreateFieldGetterFunc<TObject, TResult>(fi);
        }

        /// <summary>
        /// Creates a two argument getter delegate for a given object type {TObject}, result type {TResult}, and a selected Field <paramref name="fi"/>.  
        /// When called, this delegate will obtain and return the field's value as evaluated on the target object.
        /// </summary>
        /// <typeparam name="TObject">The actual type of the target object.  Must be an instance of the FieldInfo.DeclaringType.</typeparam>
        /// <typeparam name="TResult">The property value type.  Must be the same as the FieldInfo.FieldType, or must be an assignable from it.</typeparam>
        /// <param name="fi">The FieldInfo for the property to be read.  Must not refer to a static field.</param>
        /// <param name="permitCastAttempt">When true this allows the generation of delegates that will code the attempt to perform the cast based type conversion even if the {TResult} is not known to be assignable from the given FieldInfo.FieldType</param>
        /// <exception cref="System.NotSupportedException">Exception thrown on any validation error as described above.</exception>
        public static Func<TObject, TResult> CreateFieldGetterFunc<TObject, TResult>(FieldInfo fi, bool permitCastAttempt = false)
        {
            Type targetObjType = typeof(TObject);
            Type resultType = typeof(TResult);

            if (!targetObjType.Equals(fi.DeclaringType) && !targetObjType.IsSubclassOf(fi.DeclaringType))
                throw new System.NotSupportedException("Cannot create field getter function when given TObject is not derived from the given FieldInfo instance's DeclaringType");

            if (fi.IsStatic)
                throw new System.NotSupportedException("Cannot create field getter action when given FieldInfo refers to a static field");

            if (resultType.Equals(fi.FieldType))
            {
                ParameterExpression targetObjParameter = Expression.Parameter(targetObjType, "targetObj");
                Expression fieldGetExpr = Expression.Field(targetObjParameter, fi);
                var lambdaExpr = Expression.Lambda<Func<TObject, TResult>>(fieldGetExpr, targetObjParameter);

                Func<TObject, TResult> func = lambdaExpr.Compile();

                return func;
            }
            else if (resultType.IsAssignableFrom(fi.FieldType) || permitCastAttempt)
            {
                ParameterExpression targetObjParameter = Expression.Parameter(targetObjType, "targetObj");
                Expression fieldGetExpr = Expression.Field(targetObjParameter, fi);
                Expression convertedValueExpr = Expression.Convert(fieldGetExpr, resultType);
                var lambdaExpr = Expression.Lambda<Func<TObject, TResult>>(convertedValueExpr, targetObjParameter);

                Func<TObject, TResult> func = lambdaExpr.Compile();

                return func;
            }
            else
            {
                throw new System.NotSupportedException("Cannot create field getter action when given TResult is neither equal to, nor assignable from a reference to the given FieldInfo instance's FieldType");
            }
        }

        /// <summary>
        /// Creates a two argument setter delegate for a given object type {TObject}, value type {TValue}, and a selected Field <paramref name="fi"/>.
        /// When called, this delegate will assign the given value to the selected field in the given target object.
        /// </summary>
        /// <typeparam name="TObject">The actual type of the target object.  Must be an instance of the FieldInfo.DeclaringType and must not be a value type.</typeparam>
        /// <typeparam name="TValue">The property value type.  Must be the same as the FieldInfo.FieldType, or must be assignable to it.</typeparam>
        /// <param name="fi">The FieldInfo for the field to be set.  Must not refer to a static field.</param>
        /// <param name="permitCastAttempt">When true this allows the generation of delegates that will code the attempt to perform the cast based type conversion even if the FieldInfo.FieldType is not known to be assignable from the given {TValue} type</param>
        /// <exception cref="System.NotSupportedException">Exception thrown on any validation error as described above.</exception>
        public static Action<TObject, TValue> CreateFieldSetterAction<TObject, TValue>(FieldInfo fi, bool permitCastAttempt = false)
        {
            Type targetObjType = typeof(TObject);
            Type valueType = typeof(TValue);

            if (!targetObjType.Equals(fi.DeclaringType) && !targetObjType.IsSubclassOf(fi.DeclaringType))
                throw new System.NotSupportedException("Cannot create field setter action when given TObject is not derived from the given FieldInfo instance's DeclaringType");

            if (targetObjType.IsValueType)
                throw new System.NotSupportedException("Cannot create field setter action when given TObject is a value type");

            if (fi.IsStatic)
                throw new System.NotSupportedException("Cannot create field setter action when given FieldInfo refers to a static field");

            if (valueType.Equals(fi.FieldType))
            {
                ParameterExpression targetObjParameter = Expression.Parameter(targetObjType, "targetObj");
                ParameterExpression valueParameter = Expression.Parameter(valueType, "value");
                Expression fieldExpr = Expression.Field(targetObjParameter, fi);
                Expression assignExpr = Expression.Assign(fieldExpr, valueParameter);
                var lambdaExpr = Expression.Lambda<Action<TObject, TValue>>(assignExpr, targetObjParameter, valueParameter);

                Action<TObject, TValue> act = lambdaExpr.Compile();

                return act;
            }
            else if (fi.FieldType.IsAssignableFrom(valueType) || permitCastAttempt)
            {
                ParameterExpression targetObjParameter = Expression.Parameter(targetObjType, "targetObj");
                ParameterExpression valueParameter = Expression.Parameter(valueType, "value");
                Expression covnertedValueExpr = Expression.Convert(valueParameter, fi.FieldType);
                Expression fieldExpr = Expression.Field(targetObjParameter, fi);
                Expression assignExpr = Expression.Assign(fieldExpr, covnertedValueExpr);
                var lambdaExpr = Expression.Lambda<Action<TObject, TValue>>(assignExpr, targetObjParameter, valueParameter);

                Action<TObject, TValue> act = lambdaExpr.Compile();

                return act;
            }
            else
            {
                throw new System.NotSupportedException("Cannot create field setter action when given TValue is neither equal to, nor assignable as a reference to the given FieldInfo instance's FieldType");
            }
        }
    }
}

//-------------------------------------------------------------------
