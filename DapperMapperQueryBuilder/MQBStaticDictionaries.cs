using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Mapper;
using System.Dynamic;

namespace MQBStatic
{
    public class DMStatic_Store
    {
        static DMStatic_Store()
        {
            InitStaticSDictionary();
        }

        #region fields
        protected static readonly object _StoreLockObject = new object();
        protected static Dictionary<Type, iDapperMapper> _Mappers;
        protected static List<Type> _TypesToMap;
        #endregion

        #region helpers
        private static void InitStaticSDictionary()
        {
            if (_Mappers == null)
            {
                lock (_StoreLockObject)
                {
                    if (_Mappers == null)
                        _Mappers = new Dictionary<Type, iDapperMapper>();
                }
            }
            if (_TypesToMap == null)
            {
                lock (_StoreLockObject)
                {
                    if (_TypesToMap == null)
                        _TypesToMap = new List<Type>();
                }
            }
        }
        #endregion
    }

    public class MQBStatic_QBuilder
    {
        public MQBStatic_QBuilder(string[] customNamespaces)
        {
            if (customNamespaces != null)
            {
                lock (_LockObject)
                {
                    _CustomNamespaces = customNamespaces.ToList();
                }
            }
            else if (_CustomNamespaces == null)
            {
                lock (_LockObject)
                {
                    if (_CustomNamespaces == null)
                        _CustomNamespaces = new List<string>();
                }
            }

            InitStaticDictionaries();
        }
        public MQBStatic_QBuilder() { }

        #region fields
        protected static readonly object _LockObject = new object();
        protected static List<string> _CustomNamespaces;
        protected static Dictionary<Type, PropertyInfo[]> _QBPropertyInfos;
        protected static Dictionary<Type, FieldInfo[]> _QBFieldInfos;
        protected static Dictionary<Type, IEnumerable<string>> _NamesList;
        protected static Dictionary<Type, string> _Columns;
        #endregion

        #region helpers
        private static void InitStaticDictionaries()
        {
            if (_QBPropertyInfos == null)
            {
                lock (_LockObject)
                {
                    if (_QBPropertyInfos == null)
                        _QBPropertyInfos = new Dictionary<Type, PropertyInfo[]>();
                }
            }
            if (_QBFieldInfos == null)
            {
                lock (_LockObject)
                {
                    if (_QBFieldInfos == null)
                        _QBFieldInfos = new Dictionary<Type, FieldInfo[]>();
                }
            }
            if (_NamesList == null)
            {
                lock (_LockObject)
                {
                    if (_NamesList == null)
                        _NamesList = new Dictionary<Type, IEnumerable<string>>();
                }
            }
            if (_Columns == null)
            {
                lock (_LockObject)
                {
                    if (_Columns == null)
                        _Columns = new Dictionary<Type, string>();
                }
            }
        }
        #endregion
    }

    public class MQBStatic_IEnumerable : MQBStatic_QBuilder
    {
        public MQBStatic_IEnumerable(string[] customNamespaces = null) : base(customNamespaces)
        {
            InitStaticDDictionary();
        }

        #region fields
        protected static Dictionary<Type, Dictionary<Type, Func<dynamic, bool>>> _InterfacesToObjects;
        protected static Dictionary<Type, List<string>> _AllowDuplicates;
        #endregion

        #region helpers
        private static void InitStaticDDictionary()
        {
            if (_InterfacesToObjects == null)
            {
                lock (_LockObject)
                {
                    if (_InterfacesToObjects == null)
                        _InterfacesToObjects = new Dictionary<Type, Dictionary<Type, Func<dynamic, bool>>>();
                }
            }
            if (_AllowDuplicates == null)
            {
                lock (_LockObject)
                {
                    if (_AllowDuplicates == null)
                        _AllowDuplicates = new Dictionary<Type, List<string>>();
                }
            }
        }
        #endregion

        #region public methods
        protected Type GetMemberType(MemberInfo mInfo)
        {
            if (mInfo.MemberType == MemberTypes.Property) return ((PropertyInfo)mInfo).PropertyType;
            else return ((FieldInfo)mInfo).FieldType;
        }
        protected Type ResolveInterface(MemberInfo mInfo, IEnumerable<dynamic> dapperResult)
        {
            foreach (KeyValuePair<Type, Func<dynamic, bool>> kvp in _InterfacesToObjects[GetMemberType(mInfo)])
                if (kvp.Value(dapperResult.First()))
                    return kvp.Key;
            return null;
        }
        protected Type ResolveInterface(Type t, IEnumerable<dynamic> dapperResult)
        {
            foreach (KeyValuePair<Type, Func<dynamic, bool>> kvp in _InterfacesToObjects[t])
                if (kvp.Value(dapperResult.First()))
                    return kvp.Key;
            return null;
        }
        #endregion
    }

    public class DMStatic_Dictionary : MQBStatic_IEnumerable
    {
        public DMStatic_Dictionary(string[] customNamespaces = null) : base(customNamespaces)
        {
            InitStaticDDictionary();
        }

        #region fields
        protected static Dictionary<Type, Dictionary<string, string[]>> _Dictionaries;
        #endregion

        #region helpers
        private static void InitStaticDDictionary()
        {
            if (_Dictionaries == null)
            {
                lock (_LockObject)
                {
                    if (_Dictionaries == null)
                        _Dictionaries = new Dictionary<Type, Dictionary<string, string[]>>();
                }
            }
        }
        #endregion
    }

    public class DMStatic_Mapper : DMStatic_Dictionary
    {
        public DMStatic_Mapper(string[] customNamespaces = null) : base(customNamespaces)
        {
            InitStaticDictionaries();
        }

        #region fields
        protected static Dictionary<Type, Dictionary<MemberInfo, MemberTypeInfo>> _mtInfos;
        protected static Dictionary<Type, Delegate> _Constructors;
        protected static List<Type> _OnlyConstructor;
        protected static Dictionary<Type, Dictionary<string, Delegate>> _MembersCreators;
        protected static Dictionary<Type, List<string>> _NestedProperties;
        protected static Dictionary<Type, List<string>> _Interfaces;
        protected static Dictionary<Type, List<string>> _Ignores;
        protected static Dictionary<Type, Tuple<string[], bool>> _Prefixes;
        protected static Dictionary<Type, Tuple<string[], bool>> _Postfixes;
        #endregion

        #region helpers
        private static void InitStaticDictionaries()
        {
            if (_mtInfos == null)
            {
                lock (_LockObject)
                {
                    if (_mtInfos == null)
                        _mtInfos = new Dictionary<Type, Dictionary<MemberInfo, MemberTypeInfo>>();
                }
            }
            if (_Constructors == null)
            {
                lock (_LockObject)
                {
                    if (_Constructors == null)
                        _Constructors = new Dictionary<Type, Delegate>();
                }
            }
            if (_OnlyConstructor == null)
            {
                lock (_LockObject)
                {
                    if (_OnlyConstructor == null)
                        _OnlyConstructor = new List<Type>();
                }
            }
            if (_MembersCreators == null)
            {
                lock (_LockObject)
                {
                    if (_MembersCreators == null)
                        _MembersCreators = new Dictionary<Type, Dictionary<string, Delegate>>();
                }
            }
            if (_NestedProperties == null)
            {
                lock (_LockObject)
                {
                    if (_NestedProperties == null)
                        _NestedProperties = new Dictionary<Type, List<string>>();
                }
            }
            if (_Interfaces == null)
            {
                lock (_LockObject)
                {
                    if (_Interfaces == null)
                        _Interfaces = new Dictionary<Type, List<string>>();
                }
            }
            if (_Ignores == null)
            {
                lock (_LockObject)
                {
                    if (_Ignores == null)
                        _Ignores = new Dictionary<Type, List<string>>();
                }
            }
            if (_Prefixes == null)
            {
                lock (_LockObject)
                {
                    if (_Prefixes == null)
                        _Prefixes = new Dictionary<Type, Tuple<string[], bool>>();
                }
            }
            if (_Postfixes == null)
            {
                lock (_LockObject)
                {
                    if (_Postfixes == null)
                        _Postfixes = new Dictionary<Type, Tuple<string[], bool>>();
                }
            }            
        }
        #endregion
    }
}
