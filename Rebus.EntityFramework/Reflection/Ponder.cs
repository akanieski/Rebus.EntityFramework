using System.Linq.Expressions;

namespace Rebus.EntityFramework.Reflection;

class Reflect
{
    public static string Path<T>(Expression<Func<T, object>> expression)
    {
        return GetPropertyName(expression);
    }

    public static object Value(object obj, string path)
    {
        var dots = path.Split('.');

        foreach(var dot in dots)
        {
            var propertyInfo = obj.GetType().GetProperty(dot);
            if (propertyInfo == null) return null!;
            var o = propertyInfo.GetValue(obj, []);
            if (o == null) break;
            obj = o;
        }

        return obj;
    }

    static string GetPropertyName(Expression expression)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (expression == null) return "";

        if (expression is LambdaExpression)
        {
            expression = ((LambdaExpression) expression).Body;
        }

        if (expression is UnaryExpression)
        {
            expression = ((UnaryExpression)expression).Operand;
        }

        if (expression is MemberExpression)
        {
            dynamic memberExpression = expression;

            var lambdaExpression = (Expression)memberExpression.Expression;

            string prefix;
            if (lambdaExpression != null)
            {
                prefix = GetPropertyName(lambdaExpression);
                if (!string.IsNullOrEmpty(prefix))
                {
                    prefix += ".";
                }
            }
            else
            {
                prefix = "";
            }

            var propertyName = memberExpression.Member.Name;
                
            return prefix + propertyName;
        }

        return "";
    }
}