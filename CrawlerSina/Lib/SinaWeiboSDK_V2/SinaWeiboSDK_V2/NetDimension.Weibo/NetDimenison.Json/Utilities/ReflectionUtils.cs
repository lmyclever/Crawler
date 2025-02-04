#region License
// Copyright (c) 2007 James Newton-King
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using System.Globalization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Text;
#if NETFX_CORE
using IConvertible = NetDimension.Json.Utilities.Convertible;
#endif
#if NETFX_CORE || PORTABLE
using ICustomAttributeProvider = NetDimension.Json.Utilities.CustomAttributeProvider;
#endif
#if NET20
using NetDimension.Json.Utilities.LinqBridge;
#else
using System.Linq;
#endif
using NetDimension.Json.Serialization;

namespace NetDimension.Json.Utilities
{
#if NETFX_CORE || PORTABLE
  internal enum MemberTypes
  {
    Property,
    Field,
    Event,
    Method,
    Other
  }

  internal class CustomAttributeProvider
  {
    private readonly object _underlyingObject;

    public CustomAttributeProvider(object o)
    {
      _underlyingObject = o;
    }

    public object UnderlyingObject
    {
      get { return _underlyingObject; }
    }
  }
#endif

#if NETFX_CORE
  internal enum TypeCode
  {
    Empty,
    Object,
    String,
    Char,
    Boolean,
    SByte,
    Int16,
    UInt16,
    Int32,
    Byte,
    UInt32,
    Int64,
    UInt64,
    Single,
    Double,
    DateTime,
    Decimal
  }

  [Flags]
  internal enum BindingFlags
  {
    Default = 0,
    IgnoreCase = 1,
    DeclaredOnly = 2,
    Instance = 4,
    Static = 8,
    Public = 16,
    NonPublic = 32,
    FlattenHierarchy = 64,
    InvokeMethod = 256,
    CreateInstance = 512,
    GetField = 1024,
    SetField = 2048,
    GetProperty = 4096,
    SetProperty = 8192,
    PutDispProperty = 16384,
    ExactBinding = 65536,
    PutRefDispProperty = 32768,
    SuppressChangeType = 131072,
    OptionalParamBinding = 262144,
    IgnoreReturn = 16777216
  }
#endif

  internal static class ReflectionUtils
  {
    public static readonly Type[] EmptyTypes;

    static ReflectionUtils()
    {
#if !(NETFX_CORE || PORTABLE)
      EmptyTypes = Type.EmptyTypes;
#else
      EmptyTypes = new Type[0];
#endif
    }

    public static ICustomAttributeProvider GetCustomAttributeProvider(this object o)
    {
#if !(NETFX_CORE || PORTABLE)
      return (ICustomAttributeProvider)o;
#else
      return new ICustomAttributeProvider(o);
#endif
    }

    public static bool IsVirtual(this PropertyInfo propertyInfo)
    {
      ValidationUtils.ArgumentNotNull(propertyInfo, "propertyInfo");

      MethodInfo m = propertyInfo.GetGetMethod();
      if (m != null && m.IsVirtual)
        return true;

      m = propertyInfo.GetSetMethod();
      if (m != null && m.IsVirtual)
        return true;

      return false;
    }

    public static Type GetObjectType(object v)
    {
      return (v != null) ? v.GetType() : null;
    }

    public static string GetTypeName(Type t, FormatterAssemblyStyle assemblyFormat)
    {
      return GetTypeName(t, assemblyFormat, null);
    }

    public static string GetTypeName(Type t, FormatterAssemblyStyle assemblyFormat, SerializationBinder binder)
    {
      string fullyQualifiedTypeName;
#if !(NET20 || NET35)
      if (binder != null)
      {
        string assemblyName, typeName;
        binder.BindToName(t, out assemblyName, out typeName);
        fullyQualifiedTypeName = typeName + (assemblyName == null ? string.Empty : ", " + assemblyName);
      }
      else
      {
        fullyQualifiedTypeName = t.AssemblyQualifiedName;
      }
#else
      fullyQualifiedTypeName = t.AssemblyQualifiedName;
#endif

      switch (assemblyFormat)
      {
        case FormatterAssemblyStyle.Simple:
          return RemoveAssemblyDetails(fullyQualifiedTypeName);
        case FormatterAssemblyStyle.Full:
          return fullyQualifiedTypeName;
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    private static string RemoveAssemblyDetails(string fullyQualifiedTypeName)
    {
      StringBuilder builder = new StringBuilder();

      // loop through the type name and filter out qualified assembly details from nested type names
      bool writingAssemblyName = false;
      bool skippingAssemblyDetails = false;
      for (int i = 0; i < fullyQualifiedTypeName.Length; i++)
      {
        char current = fullyQualifiedTypeName[i];
        switch (current)
        {
          case '[':
            writingAssemblyName = false;
            skippingAssemblyDetails = false;
            builder.Append(current);
            break;
          case ']':
            writingAssemblyName = false;
            skippingAssemblyDetails = false;
            builder.Append(current);
            break;
          case ',':
            if (!writingAssemblyName)
            {
              writingAssemblyName = true;
              builder.Append(current);
            }
            else
            {
              skippingAssemblyDetails = true;
            }
            break;
          default:
            if (!skippingAssemblyDetails)
              builder.Append(current);
            break;
        }
      }

      return builder.ToString();
    }

    public static bool IsInstantiatableType(Type t)
    {
      ValidationUtils.ArgumentNotNull(t, "t");

      if (t.IsAbstract() || t.IsInterface() || t.IsArray || t.IsGenericTypeDefinition() || t == typeof(void))
        return false;

      if (!HasDefaultConstructor(t))
        return false;

      return true;
    }

    public static bool HasDefaultConstructor(Type t)
    {
      return HasDefaultConstructor(t, false);
    }

    public static bool HasDefaultConstructor(Type t, bool nonPublic)
    {
      ValidationUtils.ArgumentNotNull(t, "t");

      if (t.IsValueType())
        return true;

      return (GetDefaultConstructor(t, nonPublic) != null);
    }

    public static ConstructorInfo GetDefaultConstructor(Type t)
    {
      return GetDefaultConstructor(t, false);
    }

    public static ConstructorInfo GetDefaultConstructor(Type t, bool nonPublic)
    {
      BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public;
      if (nonPublic)
        bindingFlags = bindingFlags | BindingFlags.NonPublic;

      return t.GetConstructors(bindingFlags).SingleOrDefault(c => !c.GetParameters().Any());
    }

    public static bool IsNullable(Type t)
    {
      ValidationUtils.ArgumentNotNull(t, "t");

      if (t.IsValueType())
        return IsNullableType(t);

      return true;
    }

    public static bool IsNullableType(Type t)
    {
      ValidationUtils.ArgumentNotNull(t, "t");

      return (t.IsGenericType() && t.GetGenericTypeDefinition() == typeof(Nullable<>));
    }

    public static Type EnsureNotNullableType(Type t)
    {
      return (IsNullableType(t))
        ? Nullable.GetUnderlyingType(t)
        : t;
    }

    public static bool ImplementsGenericDefinition(Type type, Type genericInterfaceDefinition)
    {
      Type implementingType;
      return ImplementsGenericDefinition(type, genericInterfaceDefinition, out implementingType);
    }

    public static bool ImplementsGenericDefinition(Type type, Type genericInterfaceDefinition, out Type implementingType)
    {
      ValidationUtils.ArgumentNotNull(type, "type");
      ValidationUtils.ArgumentNotNull(genericInterfaceDefinition, "genericInterfaceDefinition");

      if (!genericInterfaceDefinition.IsInterface() || !genericInterfaceDefinition.IsGenericTypeDefinition())
        throw new ArgumentNullException("'{0}' is not a generic interface definition.".FormatWith(CultureInfo.InvariantCulture, genericInterfaceDefinition));

      if (type.IsInterface())
      {
        if (type.IsGenericType())
        {
          Type interfaceDefinition = type.GetGenericTypeDefinition();

          if (genericInterfaceDefinition == interfaceDefinition)
          {
            implementingType = type;
            return true;
          }
        }
      }

      foreach (Type i in type.GetInterfaces())
      {
        if (i.IsGenericType())
        {
          Type interfaceDefinition = i.GetGenericTypeDefinition();

          if (genericInterfaceDefinition == interfaceDefinition)
          {
            implementingType = i;
            return true;
          }
        }
      }

      implementingType = null;
      return false;
    }

    public static bool InheritsGenericDefinition(Type type, Type genericClassDefinition)
    {
      Type implementingType;
      return InheritsGenericDefinition(type, genericClassDefinition, out implementingType);
    }

    public static bool InheritsGenericDefinition(Type type, Type genericClassDefinition, out Type implementingType)
    {
      ValidationUtils.ArgumentNotNull(type, "type");
      ValidationUtils.ArgumentNotNull(genericClassDefinition, "genericClassDefinition");

      if (!genericClassDefinition.IsClass() || !genericClassDefinition.IsGenericTypeDefinition())
        throw new ArgumentNullException("'{0}' is not a generic class definition.".FormatWith(CultureInfo.InvariantCulture, genericClassDefinition));

      return InheritsGenericDefinitionInternal(type, genericClassDefinition, out implementingType);
    }

    private static bool InheritsGenericDefinitionInternal(Type currentType, Type genericClassDefinition, out Type implementingType)
    {
      if (currentType.IsGenericType())
      {
        Type currentGenericClassDefinition = currentType.GetGenericTypeDefinition();

        if (genericClassDefinition == currentGenericClassDefinition)
        {
          implementingType = currentType;
          return true;
        }
      }

      if (currentType.BaseType() == null)
      {
        implementingType = null;
        return false;
      }

      return InheritsGenericDefinitionInternal(currentType.BaseType(), genericClassDefinition, out implementingType);
    }

    /// <summary>
    /// Gets the type of the typed collection's items.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The type of the typed collection's items.</returns>
    public static Type GetCollectionItemType(Type type)
    {
      ValidationUtils.ArgumentNotNull(type, "type");
      Type genericListType;

      if (type.IsArray)
      {
        return type.GetElementType();
      }
      else if (ImplementsGenericDefinition(type, typeof(IEnumerable<>), out genericListType))
      {
        if (genericListType.IsGenericTypeDefinition())
          throw new Exception("Type {0} is not a collection.".FormatWith(CultureInfo.InvariantCulture, type));

        return genericListType.GetGenericArguments()[0];
      }
      else if (typeof(IEnumerable).IsAssignableFrom(type))
      {
        return null;
      }
      else
      {
        throw new Exception("Type {0} is not a collection.".FormatWith(CultureInfo.InvariantCulture, type));
      }
    }

    public static void GetDictionaryKeyValueTypes(Type dictionaryType, out Type keyType, out Type valueType)
    {
      ValidationUtils.ArgumentNotNull(dictionaryType, "type");

      Type genericDictionaryType;
      if (ImplementsGenericDefinition(dictionaryType, typeof(IDictionary<,>), out genericDictionaryType))
      {
        if (genericDictionaryType.IsGenericTypeDefinition())
          throw new Exception("Type {0} is not a dictionary.".FormatWith(CultureInfo.InvariantCulture, dictionaryType));

        Type[] dictionaryGenericArguments = genericDictionaryType.GetGenericArguments();

        keyType = dictionaryGenericArguments[0];
        valueType = dictionaryGenericArguments[1];
        return;
      }
      else if (typeof(IDictionary).IsAssignableFrom(dictionaryType))
      {
        keyType = null;
        valueType = null;
        return;
      }
      else
      {
        throw new Exception("Type {0} is not a dictionary.".FormatWith(CultureInfo.InvariantCulture, dictionaryType));
      }
    }

    public static Type GetDictionaryValueType(Type dictionaryType)
    {
      Type keyType;
      Type valueType;
      GetDictionaryKeyValueTypes(dictionaryType, out keyType, out valueType);

      return valueType;
    }

    public static Type GetDictionaryKeyType(Type dictionaryType)
    {
      Type keyType;
      Type valueType;
      GetDictionaryKeyValueTypes(dictionaryType, out keyType, out valueType);

      return keyType;
    }

    /// <summary>
    /// Gets the member's underlying type.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>The underlying type of the member.</returns>
    public static Type GetMemberUnderlyingType(MemberInfo member)
    {
      ValidationUtils.ArgumentNotNull(member, "member");

      switch (member.MemberType())
      {
        case MemberTypes.Field:
          return ((FieldInfo)member).FieldType;
        case MemberTypes.Property:
          return ((PropertyInfo)member).PropertyType;
        case MemberTypes.Event:
          return ((EventInfo)member).EventHandlerType;
        default:
          throw new ArgumentException("MemberInfo must be of type FieldInfo, PropertyInfo or EventInfo", "member");
      }
    }

    /// <summary>
    /// Determines whether the member is an indexed property.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>
    /// 	<c>true</c> if the member is an indexed property; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsIndexedProperty(MemberInfo member)
    {
      ValidationUtils.ArgumentNotNull(member, "member");

      PropertyInfo propertyInfo = member as PropertyInfo;

      if (propertyInfo != null)
        return IsIndexedProperty(propertyInfo);
      else
        return false;
    }

    /// <summary>
    /// Determines whether the property is an indexed property.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>
    /// 	<c>true</c> if the property is an indexed property; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsIndexedProperty(PropertyInfo property)
    {
      ValidationUtils.ArgumentNotNull(property, "property");

      return (property.GetIndexParameters().Length > 0);
    }

    /// <summary>
    /// Gets the member's value on the object.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="target">The target object.</param>
    /// <returns>The member's value on the object.</returns>
    public static object GetMemberValue(MemberInfo member, object target)
    {
      ValidationUtils.ArgumentNotNull(member, "member");
      ValidationUtils.ArgumentNotNull(target, "target");

      switch (member.MemberType())
      {
        case MemberTypes.Field:
          return ((FieldInfo)member).GetValue(target);
        case MemberTypes.Property:
          try
          {
            return ((PropertyInfo)member).GetValue(target, null);
          }
          catch (TargetParameterCountException e)
          {
            throw new ArgumentException("MemberInfo '{0}' has index parameters".FormatWith(CultureInfo.InvariantCulture, member.Name), e);
          }
        default:
          throw new ArgumentException("MemberInfo '{0}' is not of type FieldInfo or PropertyInfo".FormatWith(CultureInfo.InvariantCulture, CultureInfo.InvariantCulture, member.Name), "member");
      }
    }

    /// <summary>
    /// Sets the member's value on the target object.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="target">The target.</param>
    /// <param name="value">The value.</param>
    public static void SetMemberValue(MemberInfo member, object target, object value)
    {
      ValidationUtils.ArgumentNotNull(member, "member");
      ValidationUtils.ArgumentNotNull(target, "target");

      switch (member.MemberType())
      {
        case MemberTypes.Field:
          ((FieldInfo)member).SetValue(target, value);
          break;
        case MemberTypes.Property:
          ((PropertyInfo)member).SetValue(target, value, null);
          break;
        default:
          throw new ArgumentException("MemberInfo '{0}' must be of type FieldInfo or PropertyInfo".FormatWith(CultureInfo.InvariantCulture, member.Name), "member");
      }
    }

    /// <summary>
    /// Determines whether the specified MemberInfo can be read.
    /// </summary>
    /// <param name="member">The MemberInfo to determine whether can be read.</param>
    /// /// <param name="nonPublic">if set to <c>true</c> then allow the member to be gotten non-publicly.</param>
    /// <returns>
    /// 	<c>true</c> if the specified MemberInfo can be read; otherwise, <c>false</c>.
    /// </returns>
    public static bool CanReadMemberValue(MemberInfo member, bool nonPublic)
    {
      switch (member.MemberType())
      {
        case MemberTypes.Field:
          FieldInfo fieldInfo = (FieldInfo)member;

          if (nonPublic)
            return true;
          else if (fieldInfo.IsPublic)
            return true;
          return false;
        case MemberTypes.Property:
          PropertyInfo propertyInfo = (PropertyInfo) member;

          if (!propertyInfo.CanRead)
            return false;
          if (nonPublic)
            return true;
          return (propertyInfo.GetGetMethod(nonPublic) != null);
        default:
          return false;
      }
    }

    /// <summary>
    /// Determines whether the specified MemberInfo can be set.
    /// </summary>
    /// <param name="member">The MemberInfo to determine whether can be set.</param>
    /// <param name="nonPublic">if set to <c>true</c> then allow the member to be set non-publicly.</param>
    /// <param name="canSetReadOnly">if set to <c>true</c> then allow the member to be set if read-only.</param>
    /// <returns>
    /// 	<c>true</c> if the specified MemberInfo can be set; otherwise, <c>false</c>.
    /// </returns>
    public static bool CanSetMemberValue(MemberInfo member, bool nonPublic, bool canSetReadOnly)
    {
      switch (member.MemberType())
      {
        case MemberTypes.Field:
          FieldInfo fieldInfo = (FieldInfo)member;

          if (fieldInfo.IsInitOnly && !canSetReadOnly)
            return false;
          if (nonPublic)
            return true;
          else if (fieldInfo.IsPublic)
            return true;
          return false;
        case MemberTypes.Property:
          PropertyInfo propertyInfo = (PropertyInfo)member;

          if (!propertyInfo.CanWrite)
            return false;
          if (nonPublic)
            return true;
          return (propertyInfo.GetSetMethod(nonPublic) != null);
        default:
          return false;
      }
    }

    public static List<MemberInfo> GetFieldsAndProperties(Type type, BindingFlags bindingAttr)
    {
      List<MemberInfo> targetMembers = new List<MemberInfo>();

      targetMembers.AddRange(GetFields(type, bindingAttr));
      targetMembers.AddRange(GetProperties(type, bindingAttr));

      // for some reason .NET returns multiple members when overriding a generic member on a base class
      // http://forums.msdn.microsoft.com/en-US/netfxbcl/thread/b5abbfee-e292-4a64-8907-4e3f0fb90cd9/
      // filter members to only return the override on the topmost class
      // update: I think this is fixed in .NET 3.5 SP1 - leave this in for now...
      List<MemberInfo> distinctMembers = new List<MemberInfo>(targetMembers.Count);

      var groupedMembers = targetMembers.GroupBy(m => m.Name).Select(g => new { Count = g.Count(), Members = g.Cast<MemberInfo>() });
      foreach (var groupedMember in groupedMembers)
      {
        if (groupedMember.Count == 1)
        {
          distinctMembers.Add(groupedMember.Members.First());
        }
        else
        {
          var members = groupedMember.Members.Where(m => !IsOverridenGenericMember(m, bindingAttr) || m.Name == "Item");

          distinctMembers.AddRange(members);
        }
      }

      return distinctMembers;
    }

    private static bool IsOverridenGenericMember(MemberInfo memberInfo, BindingFlags bindingAttr)
    {
      MemberTypes memberType = memberInfo.MemberType();
      if (memberType != MemberTypes.Field && memberType != MemberTypes.Property)
        throw new ArgumentException("Member must be a field or property.");

      Type declaringType = memberInfo.DeclaringType;
      if (!declaringType.IsGenericType())
        return false;
      Type genericTypeDefinition = declaringType.GetGenericTypeDefinition();
      if (genericTypeDefinition == null)
        return false;
      MemberInfo[] members = genericTypeDefinition.GetMember(memberInfo.Name, bindingAttr);
      if (members.Length == 0)
        return false;
      Type memberUnderlyingType = GetMemberUnderlyingType(members[0]);
      if (!memberUnderlyingType.IsGenericParameter)
        return false;

      return true;
    }

    public static T GetAttribute<T>(ICustomAttributeProvider attributeProvider) where T : Attribute
    {
      return GetAttribute<T>(attributeProvider, true);
    }

    public static T GetAttribute<T>(ICustomAttributeProvider attributeProvider, bool inherit) where T : Attribute
    {
      T[] attributes = GetAttributes<T>(attributeProvider, inherit);

      return attributes.SingleOrDefault();
    }

#if !(NETFX_CORE)
    public static T[] GetAttributes<T>(ICustomAttributeProvider attributeProvider, bool inherit) where T : Attribute
    {
      ValidationUtils.ArgumentNotNull(attributeProvider, "attributeProvider");

      object provider;

#if !PORTABLE
      provider = attributeProvider;
#else
      provider = attributeProvider.UnderlyingObject;
#endif

      // http://hyperthink.net/blog/getcustomattributes-gotcha/
      // ICustomAttributeProvider doesn't do inheritance

      if (provider is Type)
        return (T[])((Type)provider).GetCustomAttributes(typeof(T), inherit);

      if (provider is Assembly)
        return (T[])Attribute.GetCustomAttributes((Assembly)provider, typeof(T));

      if (provider is MemberInfo)
        return (T[])Attribute.GetCustomAttributes((MemberInfo)provider, typeof(T), inherit);

#if !PORTABLE
      if (provider is Module)
        return (T[])Attribute.GetCustomAttributes((Module)provider, typeof(T), inherit);
#endif

      if (provider is ParameterInfo)
        return (T[])Attribute.GetCustomAttributes((ParameterInfo)provider, typeof(T), inherit);

#if !PORTABLE
      return (T[])attributeProvider.GetCustomAttributes(typeof(T), inherit);
#else
      throw new Exception("Cannot get attributes from '{0}'.".FormatWith(CultureInfo.InvariantCulture, provider));
#endif
    }
#else
    public static T[] GetAttributes<T>(ICustomAttributeProvider attributeProvider, bool inherit) where T : Attribute
    {
      object provider = attributeProvider.UnderlyingObject;

      if (provider is Type)
        return ((Type)provider).GetTypeInfo().GetCustomAttributes<T>(inherit).ToArray();

      if (provider is Assembly)
        return ((Assembly)provider).GetCustomAttributes<T>().ToArray();

      if (provider is MemberInfo)
        return ((MemberInfo)provider).GetCustomAttributes<T>(inherit).ToArray();

      if (provider is Module)
        return ((Module)provider).GetCustomAttributes<T>().ToArray();

      if (provider is ParameterInfo)
        return ((ParameterInfo)provider).GetCustomAttributes<T>(inherit).ToArray();

      throw new Exception("Cannot get attributes from '{0}'.".FormatWith(CultureInfo.InvariantCulture, provider));
    }
#endif

    public static Type MakeGenericType(Type genericTypeDefinition, params Type[] innerTypes)
    {
      ValidationUtils.ArgumentNotNull(genericTypeDefinition, "genericTypeDefinition");
      ValidationUtils.ArgumentNotNullOrEmpty<Type>(innerTypes, "innerTypes");
      ValidationUtils.ArgumentConditionTrue(genericTypeDefinition.IsGenericTypeDefinition(), "genericTypeDefinition", "Type {0} is not a generic type definition.".FormatWith(CultureInfo.InvariantCulture, genericTypeDefinition));

      return genericTypeDefinition.MakeGenericType(innerTypes);
    }

    public static object CreateGeneric(Type genericTypeDefinition, Type innerType, params object[] args)
    {
      return CreateGeneric(genericTypeDefinition, new [] { innerType }, args);
    }

    public static object CreateGeneric(Type genericTypeDefinition, IList<Type> innerTypes, params object[] args)
    {
      return CreateGeneric(genericTypeDefinition, innerTypes, (t, a) => CreateInstance(t, a.ToArray()), args);
    }

    public static object CreateGeneric(Type genericTypeDefinition, IList<Type> innerTypes, Func<Type, IList<object>, object> instanceCreator, params object[] args)
    {
      ValidationUtils.ArgumentNotNull(genericTypeDefinition, "genericTypeDefinition");
      ValidationUtils.ArgumentNotNullOrEmpty(innerTypes, "innerTypes");
      ValidationUtils.ArgumentNotNull(instanceCreator, "createInstance");

      Type specificType = MakeGenericType(genericTypeDefinition, innerTypes.ToArray());

      return instanceCreator(specificType, args);
    }

     public static object CreateInstance(Type type, params object[] args)
     {
       ValidationUtils.ArgumentNotNull(type, "type");

#if !PocketPC
       return Activator.CreateInstance(type, args);
#else
       // CF doesn't have a Activator.CreateInstance overload that takes args
       // lame

       if (type.IsValueType && CollectionUtils.IsNullOrEmpty<object>(args))
         return Activator.CreateInstance(type);

       ConstructorInfo[] constructors = type.GetConstructors();
       ConstructorInfo match = constructors.Where(c =>
         {
           ParameterInfo[] parameters = c.GetParameters();
           if (parameters.Length != args.Length)
             return false;

           for (int i = 0; i < parameters.Length; i++)
           {
             ParameterInfo parameter = parameters[i];
             object value = args[i];

             if (!IsCompatibleValue(value, parameter.ParameterType))
               return false;
           }

           return true;
         }).FirstOrDefault();

       if (match == null)
         throw new Exception("Could not create '{0}' with given parameters.".FormatWith(CultureInfo.InvariantCulture, type));

       return match.Invoke(args);
#endif
     }

    public static void SplitFullyQualifiedTypeName(string fullyQualifiedTypeName, out string typeName, out string assemblyName)
    {
      int? assemblyDelimiterIndex = GetAssemblyDelimiterIndex(fullyQualifiedTypeName);

      if (assemblyDelimiterIndex != null)
      {
        typeName = fullyQualifiedTypeName.Substring(0, assemblyDelimiterIndex.Value).Trim();
        assemblyName = fullyQualifiedTypeName.Substring(assemblyDelimiterIndex.Value + 1, fullyQualifiedTypeName.Length - assemblyDelimiterIndex.Value - 1).Trim();
      }
      else
      {
        typeName = fullyQualifiedTypeName;
        assemblyName = null;
      }

    }

    private static int? GetAssemblyDelimiterIndex(string fullyQualifiedTypeName)
    {
      // we need to get the first comma following all surrounded in brackets because of generic types
      // e.g. System.Collections.Generic.Dictionary`2[[System.String, mscorlib,Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089
      int scope = 0;
      for (int i = 0; i < fullyQualifiedTypeName.Length; i++)
      {
        char current = fullyQualifiedTypeName[i];
        switch (current)
        {
          case '[':
            scope++;
            break;
          case ']':
            scope--;
            break;
          case ',':
            if (scope == 0)
              return i;
            break;
        }
      }

      return null;
    }

    public static MemberInfo GetMemberInfoFromType(Type targetType, MemberInfo memberInfo)
    {
      const BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

      switch (memberInfo.MemberType())
      {
        case MemberTypes.Property:
          PropertyInfo propertyInfo = (PropertyInfo) memberInfo;

          Type[] types = propertyInfo.GetIndexParameters().Select(p => p.ParameterType).ToArray();

          return targetType.GetProperty(propertyInfo.Name, bindingAttr, null, propertyInfo.PropertyType, types, null);
        default:
          return targetType.GetMember(memberInfo.Name, memberInfo.MemberType(), bindingAttr).SingleOrDefault();
      }
    }

    public static IEnumerable<FieldInfo> GetFields(Type targetType, BindingFlags bindingAttr)
    {
      ValidationUtils.ArgumentNotNull(targetType, "targetType");

      List<MemberInfo> fieldInfos = new List<MemberInfo>(targetType.GetFields(bindingAttr));
#if !NETFX_CORE
      // Type.GetFields doesn't return inherited private fields
      // manually find private fields from base class
      GetChildPrivateFields(fieldInfos, targetType, bindingAttr);
#endif

      return fieldInfos.Cast<FieldInfo>();
    }

    private static void GetChildPrivateFields(IList<MemberInfo> initialFields, Type targetType, BindingFlags bindingAttr)
    {
      // fix weirdness with private FieldInfos only being returned for the current Type
      // find base type fields and add them to result
      if ((bindingAttr & BindingFlags.NonPublic) != 0)
      {
        // modify flags to not search for public fields
        BindingFlags nonPublicBindingAttr = bindingAttr.RemoveFlag(BindingFlags.Public);

        while ((targetType = targetType.BaseType()) != null)
        {
          // filter out protected fields
          IEnumerable<MemberInfo> childPrivateFields =
            targetType.GetFields(nonPublicBindingAttr).Where(f => f.IsPrivate).Cast<MemberInfo>();

          initialFields.AddRange(childPrivateFields);
        }
      }
    }

    public static IEnumerable<PropertyInfo> GetProperties(Type targetType, BindingFlags bindingAttr)
    {
      ValidationUtils.ArgumentNotNull(targetType, "targetType");

      List<PropertyInfo> propertyInfos = new List<PropertyInfo>(targetType.GetProperties(bindingAttr));
      GetChildPrivateProperties(propertyInfos, targetType, bindingAttr);

      // a base class private getter/setter will be inaccessable unless the property was gotten from the base class
      for (int i = 0; i < propertyInfos.Count; i++)
      {
        PropertyInfo member = propertyInfos[i];
        if (member.DeclaringType != targetType)
        {
          PropertyInfo declaredMember = (PropertyInfo)GetMemberInfoFromType(member.DeclaringType, member);
          propertyInfos[i] = declaredMember;
        }
      }

      return propertyInfos;
    }

    public static BindingFlags RemoveFlag(this BindingFlags bindingAttr, BindingFlags flag)
    {
      return ((bindingAttr & flag) == flag)
        ? bindingAttr ^ flag
        : bindingAttr;
    }

    private static void GetChildPrivateProperties(IList<PropertyInfo> initialProperties, Type targetType, BindingFlags bindingAttr)
    {
      // fix weirdness with private PropertyInfos only being returned for the current Type
      // find base type properties and add them to result
      if ((bindingAttr & BindingFlags.NonPublic) != 0)
      {
        // modify flags to not search for public fields
        BindingFlags nonPublicBindingAttr = bindingAttr.RemoveFlag(BindingFlags.Public);

        while ((targetType = targetType.BaseType()) != null)
        {
          foreach (PropertyInfo propertyInfo in targetType.GetProperties(nonPublicBindingAttr))
          {
            PropertyInfo nonPublicProperty = propertyInfo;

            // have to test on name rather than reference because instances are different
            // depending on the type that GetProperties was called on
            int index = initialProperties.IndexOf(p => p.Name == nonPublicProperty.Name);
            if (index == -1)
            {
              initialProperties.Add(nonPublicProperty);
            }
            else
            {
              // replace nonpublic properties for a child, but gotten from
              // the parent with the one from the child
              // the property gotten from the child will have access to private getter/setter
              initialProperties[index] = nonPublicProperty;
            }
          }
        }
      }
    }

    public static bool IsMethodOverridden(Type currentType, Type methodDeclaringType, string method)
    {
      bool isMethodOverriden = currentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .Any(info =>
             info.Name == method &&
             // check that the method overrides the original on DynamicObjectProxy
             info.DeclaringType != methodDeclaringType
             // todo - find out whether there is a way to do this in winrt
#if !NETFX_CORE
             && info.GetBaseDefinition().DeclaringType == methodDeclaringType
#endif
        );

      return isMethodOverriden;
    }
  }
}