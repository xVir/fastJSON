using System;
using System.Collections.Generic;
using System.Text;
#if !IOS
using System.Reflection.Emit;
#endif
using System.Reflection;
using System.Collections;
using System.Linq;

#if !SILVERLIGHT
using System.Data;
#endif
using System.Collections.Specialized;

namespace fastJSON
{
    internal struct Getters
    {
        public string Name;
        public string lcName;
        public Reflection.GenericGetter Getter;
    }

    internal enum myPropInfoType
    {
        Int,
        Long,
        String,
        Bool,
        DateTime,
        Enum,
        Guid,

        Array,
        ByteArray,
        Dictionary,
        StringKeyDictionary,
        NameValue,
        StringDictionary,
#if !SILVERLIGHT
        Hashtable,
        DataSet,
        DataTable,
#endif
        Custom,
        Unknown,
    }

    internal struct myPropInfo
    {
        public Type pt;
        public Type bt;
        public Type changeType;
        public Reflection.GenericSetter setter;
        public Reflection.GenericGetter getter;
        public Type[] GenericTypes;
        public string Name;
        public myPropInfoType Type;
        public bool CanWrite;

        public bool IsClass;
        public bool IsValueType;
        public bool IsGenericType;
        public bool IsStruct;
    }

    internal sealed class Reflection
    {
        // Sinlgeton pattern 4 from : http://csharpindepth.com/articles/general/singleton.aspx
        private static readonly Reflection instance = new Reflection();
        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static Reflection()
        {
        }
        private Reflection()
        {
        }
        public static Reflection Instance { get { return instance; } }

        internal delegate object GenericSetter(object target, object value);
        internal delegate object GenericGetter(object obj);
        private delegate object CreateObject();

		private JSONParameters parameters;

        private SafeDictionary<Type, string> _tyname = new SafeDictionary<Type, string>();
        private SafeDictionary<string, Type> _typecache = new SafeDictionary<string, Type>();
        private SafeDictionary<Type, CreateObject> _constrcache = new SafeDictionary<Type, CreateObject>();
        private SafeDictionary<Type, Getters[]> _getterscache = new SafeDictionary<Type, Getters[]>();
        private SafeDictionary<string, Dictionary<string, myPropInfo>> _propertycache = new SafeDictionary<string, Dictionary<string, myPropInfo>>();
        private SafeDictionary<Type, Type[]> _genericTypes = new SafeDictionary<Type, Type[]>();
        private SafeDictionary<Type, Type> _genericTypeDef = new SafeDictionary<Type, Type>();

        #region json custom types
        // JSON custom
        internal SafeDictionary<Type, Serialize> _customSerializer = new SafeDictionary<Type, Serialize>();
        internal SafeDictionary<Type, Deserialize> _customDeserializer = new SafeDictionary<Type, Deserialize>();
        internal object CreateCustom(string v, Type type)
        {
            Deserialize d;
            _customDeserializer.TryGetValue(type, out d);
            return d(v);
        }

        internal void RegisterCustomType(Type type, Serialize serializer, Deserialize deserializer)
        {
            if (type != null && serializer != null && deserializer != null)
            {
                _customSerializer.Add(type, serializer);
                _customDeserializer.Add(type, deserializer);
                // reset property cache
                Reflection.Instance.ResetPropertyCache();
            }
        }

        internal bool IsTypeRegistered(Type t)
        {
            if (_customSerializer.Count == 0)
                return false;
            Serialize s;
            return _customSerializer.TryGetValue(t, out s);
        }
        #endregion

        public Type GetGenericTypeDefinition(Type t)
        {
            Type tt = null;
            if (_genericTypeDef.TryGetValue(t, out tt))
                return tt;
            else
            {
                tt = t.GetGenericTypeDefinition();
                _genericTypeDef.Add(t, tt);
                return tt;
            }
        }

        public Type[] GetGenericArguments(Type t)
        {
            Type[] tt = null;
            if (_genericTypes.TryGetValue(t, out tt))
                return tt;
            else
            {
                tt = t.GetGenericArguments();
                _genericTypes.Add(t, tt);
                return tt;
            }
        }

		internal void SetParameters(JSONParameters p)
		{
			parameters = p;
		}

        public Dictionary<string, myPropInfo> Getproperties(Type type, string typename, bool customType)
        {
            Dictionary<string, myPropInfo> sd = null;
            if (_propertycache.TryGetValue(typename, out sd))
            {
                return sd;
            }
            else
            {
                sd = new Dictionary<string, myPropInfo>();
                PropertyInfo[] pr = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                foreach (PropertyInfo p in pr)
                {
                    if (p.GetIndexParameters().Length > 0)
                    {// Property is an indexer
                        continue;
                    }

					myPropInfo d = CreateMyProp(p, p.PropertyType, p.Name, customType);
                    d.setter = CreateSetMethod(type, p);
                    if (d.setter != null)
                        d.CanWrite = true;
                    d.getter = CreateGetMethod(type, p);
                    sd.Add(d.Name.ToLower(), d);
                }
                FieldInfo[] fi = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                foreach (FieldInfo f in fi)
                {
                    myPropInfo d = CreateMyProp(f, f.FieldType, f.Name, customType);
                    if (f.IsLiteral == false)
                    {
                        d.setter = CreateSetField(type, f);
                        if (d.setter != null)
                            d.CanWrite = true;
                        d.getter = CreateGetField(type, f);
                        sd.Add(d.Name.ToLower(), d);
                    }
                }

                _propertycache.Add(typename, sd);
                return sd;
            }
        }

        private myPropInfo CreateMyProp(MemberInfo memberInfo, Type t, string name, bool customType)
        {
            myPropInfo d = new myPropInfo();
            myPropInfoType d_type = myPropInfoType.Unknown;

            if (t == typeof(int) || t == typeof(int?)) d_type = myPropInfoType.Int;
            else if (t == typeof(long) || t == typeof(long?)) d_type = myPropInfoType.Long;
            else if (t == typeof(string)) d_type = myPropInfoType.String;
            else if (t == typeof(bool) || t == typeof(bool?)) d_type = myPropInfoType.Bool;
            else if (t == typeof(DateTime) || t == typeof(DateTime?)) d_type = myPropInfoType.DateTime;
            else if (t.IsEnum) d_type = myPropInfoType.Enum;
            else if (t == typeof(Guid) || t == typeof(Guid?)) d_type = myPropInfoType.Guid;
            else if (t == typeof(StringDictionary)) d_type = myPropInfoType.StringDictionary;
            else if (t == typeof(NameValueCollection)) d_type = myPropInfoType.NameValue;
            else if (t.IsArray)
            {
                d.bt = t.GetElementType();
                if (t == typeof(byte[]))
                    d_type = myPropInfoType.ByteArray;
                else
                    d_type = myPropInfoType.Array;
            }
            else if (t.Name.Contains("Dictionary"))
            {
                d.GenericTypes = Reflection.Instance.GetGenericArguments(t);// t.GetGenericArguments();
                if (d.GenericTypes.Length > 0 && d.GenericTypes[0] == typeof(string))
                    d_type = myPropInfoType.StringKeyDictionary;
                else
                    d_type = myPropInfoType.Dictionary;
            }
#if !SILVERLIGHT
            else if (t == typeof(Hashtable)) d_type = myPropInfoType.Hashtable;
            else if (t == typeof(DataSet)) d_type = myPropInfoType.DataSet;
            else if (t == typeof(DataTable)) d_type = myPropInfoType.DataTable;
#endif
            else if (customType)
                d_type = myPropInfoType.Custom;

            if (t.IsValueType && !t.IsPrimitive && !t.IsEnum && t != typeof(decimal))
                d.IsStruct = true;

            d.IsClass = t.IsClass;
            d.IsValueType = t.IsValueType;
            if (t.IsGenericType)
            {
                d.IsGenericType = true;
                d.bt = t.GetGenericArguments()[0];
            }

			var jsonName = name;
			if (memberInfo.IsDefined(typeof(JsonPropertyAttribute), false)) {
				var attr = memberInfo.GetCustomAttributes(typeof(JsonPropertyAttribute), false)[0] as JsonPropertyAttribute;
				if (!string.IsNullOrEmpty(attr.PropertyName)) {
					jsonName = attr.PropertyName;
				}
			}

            d.pt = t;
            d.Name = jsonName;
            d.changeType = GetChangeType(t);
            d.Type = d_type;

            return d;
        }

        private Type GetChangeType(Type conversionType)
        {
            if (conversionType.IsGenericType && conversionType.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
                return Reflection.Instance.GetGenericArguments(conversionType)[0];// conversionType.GetGenericArguments()[0];

            return conversionType;
        }

        #region [   PROPERTY GET SET   ]

        internal string GetTypeAssemblyName(Type t)
        {
            string val = "";
            if (_tyname.TryGetValue(t, out val))
                return val;
            else
            {
                string s = t.AssemblyQualifiedName;
                _tyname.Add(t, s);
                return s;
            }
        }

        internal Type GetTypeFromCache(string typename)
        {
            Type val = null;
            if (_typecache.TryGetValue(typename, out val))
                return val;
            else
            {
                Type t = Type.GetType(typename);
                //if (t == null) // RaptorDB : loading runtime assemblies
                //{
                //    t = Type.GetType(typename, (name) => {
                //        return AppDomain.CurrentDomain.GetAssemblies().Where(z => z.FullName == name.FullName).FirstOrDefault();
                //    }, null, true);
                //}
                _typecache.Add(typename, t);
                return t;
            }
        }

        internal object FastCreateInstance(Type objtype)
        {
            try
            {
                CreateObject c = null;
                if (_constrcache.TryGetValue(objtype, out c))
                {
                    return c();
                }
                else
                {
					if (parameters.WithoutDynamicMethodsGeneration) {
						c = () => {
							var instance = Activator.CreateInstance(objtype);
							return instance;
						};
						_constrcache.Add(objtype, c);
					}
					else 
					{
	                    if (objtype.IsClass)
	                    {
							c = CreateClassDynamicActivator(objtype);
	                        _constrcache.Add(objtype, c);
	                    }
	                    else // structs
	                    {
							c = CreateDynamicStructActivator (objtype);
	                        _constrcache.Add(objtype, c);
	                    }
					}

                    return c();
                }
            }
            catch (Exception exc)
            {
                throw new Exception(string.Format("Failed to fast create instance for type '{0}' from assembly '{1}'",
                    objtype.FullName, objtype.AssemblyQualifiedName), exc);
            }
        }

		static CreateObject CreateClassDynamicActivator (Type objtype)
		{
			#if IOS
			throw new NotImplementedException("Dynamic code can't be used on iOS");
			#else
			var dynMethod = new DynamicMethod ("_", objtype, null);
			var ilGen = dynMethod.GetILGenerator ();
			ilGen.Emit (OpCodes.Newobj, objtype.GetConstructor (Type.EmptyTypes));
			ilGen.Emit (OpCodes.Ret);
			var c = (CreateObject)dynMethod.CreateDelegate (typeof(CreateObject));
			return c;
			#endif
		}

		static CreateObject CreateDynamicStructActivator (Type objtype)
		{
			#if IOS
			throw new NotImplementedException("Dynamic code can't be used on iOS");
			#else
			var dynMethod = new DynamicMethod ("_", typeof(object), null);
			var ilGen = dynMethod.GetILGenerator ();
			var lv = ilGen.DeclareLocal (objtype);
			ilGen.Emit (OpCodes.Ldloca_S, lv);
			ilGen.Emit (OpCodes.Initobj, objtype);
			ilGen.Emit (OpCodes.Ldloc_0);
			ilGen.Emit (OpCodes.Box, objtype);
			ilGen.Emit (OpCodes.Ret);
			var c = (CreateObject)dynMethod.CreateDelegate (typeof(CreateObject));
			return c;
			#endif
		}

        internal GenericSetter CreateSetField(Type type, FieldInfo fieldInfo)
        {
			if (parameters.WithoutDynamicMethodsGeneration) 
			{
				return (target, val) =>
				{
					fieldInfo.SetValue(target, val);
					return target;
				};	
   			}
			else
			{
				return CreateDynamicSetField (type, fieldInfo);
			}
        }

		static GenericSetter CreateDynamicSetField(Type type, FieldInfo fieldInfo)
		{
			#if IOS
			throw new NotImplementedException("Dynamic code can't be used on iOS");
			#else
			var arguments = new Type[2];
			arguments [0] = arguments [1] = typeof(object);
			var dynamicSet = new DynamicMethod ("_", typeof(object), arguments, type);
			var il = dynamicSet.GetILGenerator ();
			if (!type.IsClass)// structs
			 {
				var lv = il.DeclareLocal (type);
				il.Emit (OpCodes.Ldarg_0);
				il.Emit (OpCodes.Unbox_Any, type);
				il.Emit (OpCodes.Stloc_0);
				il.Emit (OpCodes.Ldloca_S, lv);
				il.Emit (OpCodes.Ldarg_1);
				if (fieldInfo.FieldType.IsClass)
					il.Emit (OpCodes.Castclass, fieldInfo.FieldType);
				else
					il.Emit (OpCodes.Unbox_Any, fieldInfo.FieldType);
				il.Emit (OpCodes.Stfld, fieldInfo);
				il.Emit (OpCodes.Ldloc_0);
				il.Emit (OpCodes.Box, type);
				il.Emit (OpCodes.Ret);
			}
			else {
				il.Emit (OpCodes.Ldarg_0);
				il.Emit (OpCodes.Ldarg_1);
				if (fieldInfo.FieldType.IsValueType)
					il.Emit (OpCodes.Unbox_Any, fieldInfo.FieldType);
				il.Emit (OpCodes.Stfld, fieldInfo);
				il.Emit (OpCodes.Ldarg_0);
				il.Emit (OpCodes.Ret);
			}
			return (GenericSetter)dynamicSet.CreateDelegate (typeof(GenericSetter));
			#endif
		}

        internal GenericSetter CreateSetMethod(Type type, PropertyInfo propertyInfo)
        {
            var setMethod = propertyInfo.GetSetMethod();
            if (setMethod == null)
			{
				return null;
			}

			if(parameters.WithoutDynamicMethodsGeneration)
			{
				return (target, val) =>
				{
					propertyInfo.SetValue(target, val, null);
					return target;
				};
			}
			else
			{
				return CreateDynamicSetMethod (type, propertyInfo);
			}
        }

		static GenericSetter CreateDynamicSetMethod(Type type, PropertyInfo propertyInfo)
		{
			#if IOS
			throw new NotImplementedException("Dynamic code can't be used on iOS");
			#else
			var setMethod = propertyInfo.GetSetMethod();

			var arguments = new Type[2];
			arguments [0] = arguments [1] = typeof(object);
			var setter = new DynamicMethod ("_", typeof(object), arguments);
			ILGenerator il = setter.GetILGenerator ();
			if (!type.IsClass)// structs
			 {
				var lv = il.DeclareLocal (type);
				il.Emit (OpCodes.Ldarg_0);
				il.Emit (OpCodes.Unbox_Any, type);
				il.Emit (OpCodes.Stloc_0);
				il.Emit (OpCodes.Ldloca_S, lv);
				il.Emit (OpCodes.Ldarg_1);
				if (propertyInfo.PropertyType.IsClass)
					il.Emit (OpCodes.Castclass, propertyInfo.PropertyType);
				else
					il.Emit (OpCodes.Unbox_Any, propertyInfo.PropertyType);
				il.EmitCall (OpCodes.Call, setMethod, null);
				il.Emit (OpCodes.Ldloc_0);
				il.Emit (OpCodes.Box, type);
			}
			else {
				il.Emit (OpCodes.Ldarg_0);
				il.Emit (OpCodes.Castclass, propertyInfo.DeclaringType);
				il.Emit (OpCodes.Ldarg_1);
				if (propertyInfo.PropertyType.IsClass)
					il.Emit (OpCodes.Castclass, propertyInfo.PropertyType);
				else
					il.Emit (OpCodes.Unbox_Any, propertyInfo.PropertyType);
				il.EmitCall (OpCodes.Callvirt, setMethod, null);
				il.Emit (OpCodes.Ldarg_0);
			}
			il.Emit (OpCodes.Ret);
			return (GenericSetter)setter.CreateDelegate (typeof(GenericSetter));
			#endif
		}

        internal GenericGetter CreateGetField(Type type, FieldInfo fieldInfo)
        {
			if (parameters.WithoutDynamicMethodsGeneration) 
			{
				return (o) => { return fieldInfo.GetValue(o); };
   			}
			else
			{
				return CreateDynamicGetField (type, fieldInfo);
			}
        }

		static GenericGetter CreateDynamicGetField (Type type, FieldInfo fieldInfo)
		{
			#if IOS
			throw new NotImplementedException("Dynamic code can't be used on iOS");
			#else
			var dynamicGet = new DynamicMethod ("_", typeof(object), new Type[] {
				typeof(object)
			}, type);
			ILGenerator il = dynamicGet.GetILGenerator ();
			if (!type.IsClass)// structs
			 {
				var lv = il.DeclareLocal (type);
				il.Emit (OpCodes.Ldarg_0);
				il.Emit (OpCodes.Unbox_Any, type);
				il.Emit (OpCodes.Stloc_0);
				il.Emit (OpCodes.Ldloca_S, lv);
				il.Emit (OpCodes.Ldfld, fieldInfo);
				if (fieldInfo.FieldType.IsValueType)
					il.Emit (OpCodes.Box, fieldInfo.FieldType);
			}
			else {
				il.Emit (OpCodes.Ldarg_0);
				il.Emit (OpCodes.Ldfld, fieldInfo);
				if (fieldInfo.FieldType.IsValueType)
					il.Emit (OpCodes.Box, fieldInfo.FieldType);
			}
			il.Emit (OpCodes.Ret);
			return (GenericGetter)dynamicGet.CreateDelegate (typeof(GenericGetter));
			#endif
		}

        internal GenericGetter CreateGetMethod(Type type, PropertyInfo propertyInfo)
        {
            var getMethod = propertyInfo.GetGetMethod();
            if (getMethod == null)
			{
				return null;
			}

			if (parameters.WithoutDynamicMethodsGeneration) 
			{
				return (o) => { return getMethod.Invoke(o, null); };
   			}
			else
			{
				return CreateDynamicGetMethod(type, propertyInfo);
			}
        }

		static GenericGetter CreateDynamicGetMethod(Type type, PropertyInfo propertyInfo)
		{
			#if IOS
			throw new NotImplementedException("Dynamic code can't be used on iOS");
			#else
			var getMethod = propertyInfo.GetGetMethod();
			DynamicMethod getter = new DynamicMethod ("_", typeof(object), new Type[] {
				typeof(object)
			}, type);
			ILGenerator il = getter.GetILGenerator ();
			if (!type.IsClass)// structs
			 {
				var lv = il.DeclareLocal (type);
				il.Emit (OpCodes.Ldarg_0);
				il.Emit (OpCodes.Unbox_Any, type);
				il.Emit (OpCodes.Stloc_0);
				il.Emit (OpCodes.Ldloca_S, lv);
				il.EmitCall (OpCodes.Call, getMethod, null);
				if (propertyInfo.PropertyType.IsValueType)
					il.Emit (OpCodes.Box, propertyInfo.PropertyType);
			}
			else {
				il.Emit (OpCodes.Ldarg_0);
				il.Emit (OpCodes.Castclass, propertyInfo.DeclaringType);
				il.EmitCall (OpCodes.Callvirt, getMethod, null);
				if (propertyInfo.PropertyType.IsValueType)
					il.Emit (OpCodes.Box, propertyInfo.PropertyType);
			}
			il.Emit (OpCodes.Ret);
			return (GenericGetter)getter.CreateDelegate (typeof(GenericGetter));
			#endif
		}

        internal Getters[] GetGetters(Type type, bool ShowReadOnlyProperties, List<Type> IgnoreAttributes)//JSONParameters param)
        {
            Getters[] val = null;
            if (_getterscache.TryGetValue(type, out val))
                return val;

            PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            List<Getters> getters = new List<Getters>();
            foreach (PropertyInfo p in props)
            {
                if (p.GetIndexParameters().Length > 0)
                {// Property is an indexer
                    continue;
                }
                if (!p.CanWrite && ShowReadOnlyProperties == false) continue;
                if (IgnoreAttributes != null)
                {
                    bool found = false;
                    foreach (var ignoreAttr in IgnoreAttributes)
                    {
                        if (p.IsDefined(ignoreAttr, false))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found)
                        continue;
                }

				var propertyName = p.Name;

				if (p.IsDefined(typeof(JsonPropertyAttribute), false)) {
					var attr = p.GetCustomAttributes(typeof(JsonPropertyAttribute), false)[0] as JsonPropertyAttribute;
					if (!string.IsNullOrEmpty(attr.PropertyName)) {
						 propertyName = attr.PropertyName;
					}
				}

                GenericGetter g = CreateGetMethod(type, p);
                
				if (g != null)
				{
					getters.Add(new Getters { Getter = g, Name = propertyName, lcName = propertyName.ToLower() });
				}
            }

            FieldInfo[] fi = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);
            foreach (var f in fi)
            {
                if (IgnoreAttributes != null)
                {
                    bool found = false;
                    foreach (var ignoreAttr in IgnoreAttributes)
                    {
                        if (f.IsDefined(ignoreAttr, false))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found)
                        continue;
                }
                if (f.IsLiteral == false)
                {
                    GenericGetter g = CreateGetField(type, f);
                    if (g != null)
                        getters.Add(new Getters { Getter = g, Name = f.Name, lcName = f.Name.ToLower() });
                }
            }
            val = getters.ToArray();
            _getterscache.Add(type, val);
            return val;
        }

        #endregion

        internal void ResetPropertyCache()
        {
            _propertycache = new SafeDictionary<string, Dictionary<string, myPropInfo>>();
        }

        internal void ClearReflectionCache()
        {
            _tyname = new SafeDictionary<Type, string>();
            _typecache = new SafeDictionary<string, Type>();
            _constrcache = new SafeDictionary<Type, CreateObject>();
            _getterscache = new SafeDictionary<Type, Getters[]>();
            _propertycache = new SafeDictionary<string, Dictionary<string, myPropInfo>>();
            _genericTypes = new SafeDictionary<Type, Type[]>();
            _genericTypeDef = new SafeDictionary<Type, Type>();
        }
    }
}
