using System;
using System.Reflection;
using System.Linq;

namespace FluentMongo.Linq
{
    using System.Runtime.Serialization;

    /// <summary>
    /// 
    /// </summary>
    internal static class ReflectionExtensions
    {
        /// <summary>
        /// Gets the custom attribute.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="member">The member.</param>
        /// <param name="inherit">if set to <c>true</c> [inherit].</param>
        /// <returns></returns>
        public static T GetCustomAttribute<T>(this MemberInfo member, bool inherit) where T : Attribute
        {
            var atts = member.GetCustomAttributes(typeof(T), inherit);
            if (atts.Length > 0)
                return (T)atts[0];

            return null;
        }

        /// <summary>
        /// Gets the return type of the member.
        /// </summary>
        /// <param name="member">The member.</param>
        /// <returns></returns>
        public static Type GetReturnType(this MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;
            }

            throw new NotSupportedException("Only fields, properties, and methods are supported.");
        }

        public static bool IsAnonymous(this Type type)
        {
            return type.Namespace == null && type.IsSealed;
        }

        /// <summary>
        /// Determines whether [is open type assignable from] [the specified open type].
        /// </summary>
        /// <param name="openType">Type of the open.</param>
        /// <param name="closedType">Type of the closed.</param>
        /// <returns>
        /// 	<c>true</c> if [is open type assignable from] [the specified open type]; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsOpenTypeAssignableFrom(this Type openType, Type closedType)
        {
            if (!openType.IsGenericTypeDefinition)
                throw new ArgumentException("Must be an open generic type.", "openType");
            if (!closedType.IsGenericType || closedType.IsGenericTypeDefinition)
                return false;

            var openArgs = openType.GetGenericArguments();
            var closedArgs = closedType.GetGenericArguments();
            if (openArgs.Length != closedArgs.Length)
                return false;
            try
            {
                var newType = openType.MakeGenericType(closedArgs);
                return newType.IsAssignableFrom(closedType);
            }
            catch
            {
                //we don't really care here, it just means the answer is false.
                return false;
            }
        }

        /// <summary>
        /// Gets the generic method.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="bindingFlags">The binding flags.</param>
        /// <param name="genericArguments">The generic arguments.</param>
        /// <param name="parameterTypes">The parameter types.</param>
        /// <returns></returns>
        public static MethodInfo GetGenericMethod(this Type type, string methodName, BindingFlags bindingFlags, Type[] genericArguments, Type[] parameterTypes)
        {
            var methods = from genericMethod in type.GetMethods(bindingFlags)
                          where genericMethod.Name == methodName && genericMethod.IsGenericMethodDefinition 
                            && genericMethod.GetGenericArguments().Count() == genericArguments.Count()
                          let method = genericMethod.MakeGenericMethod(genericArguments)
                          where method.GetParameters().Select(x => x.ParameterType).SequenceEqual(parameterTypes)
                          select method;

            return methods.SingleOrDefault();
        }

        public static Type GetInterfaceClosing(this Type type, Type openType)
        {
            if (!openType.IsGenericTypeDefinition)
                throw new ArgumentException("Must be an open generic type.", "openType");

            return type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == openType);
        }

        public static string ResolveName(this MemberInfo member)
        {
            string name = null;
            var dataMemberAttribute = member.GetCustomAttribute<DataMemberAttribute>(true);

            if (dataMemberAttribute != null)
            {
                name = dataMemberAttribute.Name;
            }
            if (string.IsNullOrEmpty(name))
            {
                name = member.Name;
            }
            return name;
        }
    }
}