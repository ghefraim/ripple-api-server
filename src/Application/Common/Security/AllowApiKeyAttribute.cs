namespace Application.Common.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class AllowApiKeyAttribute : Attribute
{
    public AllowApiKeyAttribute()
    {
    }
}