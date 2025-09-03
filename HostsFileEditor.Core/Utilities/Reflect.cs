using System.Linq.Expressions;

namespace HostsFileEditor.Utilities;

public static class Reflect
{
    public static string GetPropertyName<T>(this Expression<Func<T>> property)
    {
        var lambda = (LambdaExpression)property;
        MemberExpression memberExpression = lambda.Body is UnaryExpression unaryExpression ? (MemberExpression)unaryExpression.Operand : (MemberExpression)lambda.Body;
        return memberExpression.Member.Name;
    }
}
