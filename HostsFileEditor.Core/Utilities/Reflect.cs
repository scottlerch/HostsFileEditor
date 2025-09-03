using System.Linq.Expressions;

namespace HostsFileEditor.Utilities;

public static class Reflect
{
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
