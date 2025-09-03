// <copyright file="Reflect.cs" company="N/A">
// Copyright 2025 Scott M. Lerch
// 
// This file is part of HostsFileEditor.
// 
// HostsFileEditor is free software: you can redistribute it and/or modify it 
// under the terms of the GNU General Public License as published by the Free 
// Software Foundation, either version 2 of the License, or (at your option)
// any later version.
// 
// HostsFileEditor is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
// more details.
// 
// You should have received a copy of the GNU General Public   License along
// with HostsFileEditor. If not, see http://www.gnu.org/licenses/.
// </copyright>

using System.Linq.Expressions;

namespace HostsFileEditor.Utilities;

/// <summary>
/// Helper class to perform reflection.
/// </summary>
internal static class Reflect
{
    /// <summary>
    /// The get property name.
    /// </summary>
    /// <param name="property">
    /// The property.
    /// </param>
    /// <typeparam name="T">
    /// Type of object to reflect.
    /// </typeparam>
    /// <returns>
    /// The property name.
    /// </returns>
    public static string GetPropertyName<T>(this Expression<Func<T>> property)
    {
        var lambda = (LambdaExpression)property;
        MemberExpression memberExpression;

        if (lambda.Body is UnaryExpression unaryExpression)
        {
            memberExpression = (MemberExpression)unaryExpression.Operand;
        }
        else
        {
            memberExpression = (MemberExpression)lambda.Body;
        }

        return memberExpression.Member.Name;
    }
}