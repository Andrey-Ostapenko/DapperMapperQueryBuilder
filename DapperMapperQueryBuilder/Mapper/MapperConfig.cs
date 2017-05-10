using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections;
using MQBStatic;
using Exceptions;

namespace Mapper
{
    public class MapperConfig : DMStatic_Mapper
    {
        private MapperConfig() { }
        //TODO: ¿Es necesario customNamespaces???? Recuerda StoreType(type t)
        public MapperConfig(string[] customNamespaces) : base(customNamespaces) { }

        #region helpers
        private string GetPropertyReturnName<T, TMember>(Expression<Func<T, TMember>> expression)
        {
            //http://stackoverflow.com/questions/273941/get-property-name-and-type-using-lambda-expression
            var member = expression.Body as MemberExpression;
            if (member != null)
                return member.Member.Name;

            throw new CustomException_MapperConfig($"MapperConfig.GetPropertyReturnName:Expression {expression} is not a member access");
        }
        private void StoreType(Type t)
        {
            MapperStore store = new MapperStore();
            store.StoreType(t);
        }
        private void RemoveMember(MemberInfo minfo, List<PropertyInfo> pInfos, List<FieldInfo> fInfos)
        {
            if (minfo.MemberType == MemberTypes.Property) pInfos.Remove((PropertyInfo)minfo);
            else fInfos.Remove((FieldInfo)minfo);
        }
        /// <summary>
        /// Union static dictionaries so nested type inherit base type configuration, aside of proper nested type configuration.
        /// </summary>
        /// <param name="baseT"></param>
        /// <param name="nestedT"></param>
        private void CopyConfigurations(Type baseT, Type nestedT)
        {
            lock(_LockObject)
            {
                //_AllowDuplicates
                if (_AllowDuplicates.ContainsKey(baseT))
                {
                    if (!_AllowDuplicates.ContainsKey(nestedT))
                        _AllowDuplicates.Add(nestedT, _AllowDuplicates[baseT]);
                    else
                        _AllowDuplicates[nestedT] = _AllowDuplicates[nestedT].Union(_AllowDuplicates[baseT]).ToList();
                }
                //_Dictionaries
                if (_Dictionaries.ContainsKey(baseT))
                {
                    if (!_Dictionaries.ContainsKey(nestedT))
                        _Dictionaries.Add(nestedT, _Dictionaries[baseT]);
                    else
                        _Dictionaries[nestedT] = _Dictionaries[nestedT].Union(_Dictionaries[baseT]).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
                //_Constructors => NO. Nested constructor should handle this
                //_MembersCreators
                if (_MembersCreators.ContainsKey(baseT))
                {
                    if (!_MembersCreators.ContainsKey(nestedT))
                        _MembersCreators.Add(nestedT, _MembersCreators[baseT]);
                    else
                        _MembersCreators[nestedT] = _MembersCreators[nestedT].Union(_MembersCreators[baseT]).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
                //_NestedProperties
                if (_NestedProperties.ContainsKey(baseT))
                {
                    if (!_NestedProperties.ContainsKey(nestedT))
                        _NestedProperties.Add(nestedT, _NestedProperties[baseT]);
                    else
                        _NestedProperties[nestedT] = _NestedProperties[nestedT].Union(_NestedProperties[baseT]).ToList();
                }
                //_Prefixes
                if (_Prefixes.ContainsKey(baseT))
                {
                    if (!_Prefixes.ContainsKey(nestedT))
                        _Prefixes.Add(nestedT, _Prefixes[baseT]);
                    else
                    {
                        string[] nestedStr = _Prefixes[nestedT].Item1;
                        nestedStr = nestedStr.Union(_Prefixes[baseT].Item1).ToArray();
                        //Exclusive prefixes always stay as setted in nested type
                        _Prefixes[nestedT] = new Tuple<string[], bool>(nestedStr, _Prefixes[nestedT].Item2);
                    }
                }
                //_Postfixes
                if (_Postfixes.ContainsKey(baseT))
                {
                    if (!_Postfixes.ContainsKey(nestedT))
                        _Postfixes.Add(nestedT, _Postfixes[baseT]);
                    else
                    {
                        string[] nestedStr = _Postfixes[nestedT].Item1;
                        nestedStr = nestedStr.Union(_Postfixes[baseT].Item1).ToArray();
                        //Exclusive prefixes always stay as setted in nested type
                        _Postfixes[nestedT] = new Tuple<string[], bool>(nestedStr, _Postfixes[nestedT].Item2);
                    }
                }
                //_Interfaces
                if (_Interfaces.ContainsKey(baseT))
                {
                    if (!_Interfaces.ContainsKey(nestedT))
                        _Interfaces.Add(nestedT, _Interfaces[baseT]);
                    else
                        _Interfaces[nestedT] = _Interfaces[nestedT].Union(_Interfaces[baseT]).ToList();
                }
            }
        }
        private void SetMembersInformation(Type t)
        {
            var pInfos = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(pInfo => pInfo.GetSetMethod() != null)
                .ToList();
            var fInfos = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                //http://stackoverflow.com/questions/40820102/reflection-returns-backing-fields-of-read-only-properties
                .Where(fInfo => fInfo.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                .ToList();

            //Get all inherited fields up the hierarchy => BindingFlags.FlattenHierarchy only works with public members
            bool inheritance = t.BaseType != null;
            Type inheritedT = t;
            Type baseT;
            while (inheritance)
            {
                //inherited fields
                baseT = inheritedT.BaseType;
                var baseFInfos = baseT.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    .Where(fInfo => fInfo.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                    .ToList();
                fInfos = fInfos.Union(baseFInfos).ToList();

                //inherit mapper configurations
                MapperStore store = new MapperStore();
                if (store.GetMapper(baseT) != null)
                    CopyConfigurations(baseT, t);

                inheritance = baseT.BaseType != null;
                inheritedT = baseT;
            }

            IEnumerable<MemberInfo> mInfos = pInfos.Union((IEnumerable<MemberInfo>)fInfos);
            Dictionary<MemberInfo, MemberTypeInfo> preMTInfos = (Dictionary<MemberInfo, MemberTypeInfo>)mInfos
                .Select(x => new KeyValuePair<MemberInfo, MemberTypeInfo>(x, MemberTypeInfo.BuiltIn))
                .ToDictionary(x => x.Key, x => x.Value);

            //prevent collection was modified exception
            Dictionary<MemberInfo, MemberTypeInfo> changes = new Dictionary<MemberInfo, MemberTypeInfo>(preMTInfos);

            IEnumerable<string> preNamesList = pInfos
                .Where(pInfo => preMTInfos[pInfo] != MemberTypeInfo.IEnumerable)
                .Select(pInfo => pInfo.Name)
                .Union(fInfos
                    .Where(pInfo => preMTInfos[pInfo] != MemberTypeInfo.IEnumerable)
                    .Select(fInfo => fInfo.Name));

            //Store all MemberTypeInfo
            //Trying to save iterations doing first if dictionary.contains(type)
            if (_MembersCreators.ContainsKey(t) && _NestedProperties.ContainsKey(t))
            {
                //Set members type dictionary
                foreach (KeyValuePair<MemberInfo, MemberTypeInfo> kvp in preMTInfos)
                {
                    if (_MembersCreators[t].ContainsKey(kvp.Key.Name))
                    {
                        changes[kvp.Key] = MemberTypeInfo.Creator;//_preMTInfos[kvp.Key] = MemberTypeInfo.Creator;
                        RemoveMember(kvp.Key, pInfos, fInfos);
                    }
                    else
                    {
                        if (_NestedProperties[t].Contains(kvp.Key.Name))
                        {
                            changes[kvp.Key] = MemberTypeInfo.Nested;//_preMTInfos[kvp.Key] = MemberTypeInfo.Nested;
                            RemoveMember(kvp.Key, pInfos, fInfos);
                        }

                        Type mType = GetMemberType(kvp.Key);
                        if (typeof(IEnumerable).IsAssignableFrom(mType) && !typeof(string).IsAssignableFrom(mType))
                        {
                            changes[kvp.Key] = changes[kvp.Key] | MemberTypeInfo.IEnumerable; //_preMTInfos[kvp.Key] = _preMTInfos[kvp.Key] | MemberTypeInfo.IEnumerable;
                            RemoveMember(kvp.Key, pInfos, fInfos);
                        }
                    }
                }
            }
            else if (_MembersCreators.ContainsKey(t))
            {
                //Set members type dictionary
                foreach (KeyValuePair<MemberInfo, MemberTypeInfo> kvp in preMTInfos)
                {
                    if (_MembersCreators[t].ContainsKey(kvp.Key.Name))
                    {
                        changes[kvp.Key] = MemberTypeInfo.Creator; //_preMTInfos[kvp.Key] = MemberTypeInfo.Creator;
                        RemoveMember(kvp.Key, pInfos, fInfos);
                    }
                    else
                    {
                        Type mType = GetMemberType(kvp.Key);
                        if (typeof(IEnumerable).IsAssignableFrom(mType) && !typeof(string).IsAssignableFrom(mType))
                        {
                            changes[kvp.Key] = changes[kvp.Key] | MemberTypeInfo.IEnumerable; //_preMTInfos[kvp.Key] = _preMTInfos[kvp.Key] | MemberTypeInfo.IEnumerable;
                            RemoveMember(kvp.Key, pInfos, fInfos);
                        }
                    }
                }
            }
            else if (_NestedProperties.ContainsKey(t))
            {
                //Add to members names list
                preNamesList = preNamesList.Union(_NestedProperties[t]);

                //Set members type dictionary
                foreach (KeyValuePair<MemberInfo, MemberTypeInfo> kvp in preMTInfos)
                {
                    if (_NestedProperties[t].Contains(kvp.Key.Name))
                    {
                        changes[kvp.Key] = MemberTypeInfo.Nested; //_preMTInfos[kvp.Key] = MemberTypeInfo.Nested;
                        RemoveMember(kvp.Key, pInfos, fInfos);
                    }

                    Type mType = GetMemberType(kvp.Key);
                    if (typeof(IEnumerable).IsAssignableFrom(mType) && !typeof(string).IsAssignableFrom(mType))
                    {
                        changes[kvp.Key] = changes[kvp.Key] | MemberTypeInfo.IEnumerable; //_preMTInfos[kvp.Key] = _preMTInfos[kvp.Key] | MemberTypeInfo.IEnumerable;
                        RemoveMember(kvp.Key, pInfos, fInfos);
                    }
                }
            }
            else
            {
                //Set members type dictionary
                foreach (KeyValuePair<MemberInfo, MemberTypeInfo> kvp in preMTInfos)
                {
                    Type mType = GetMemberType(kvp.Key);
                    if (typeof(IEnumerable).IsAssignableFrom(mType) && !typeof(string).IsAssignableFrom(mType))
                        changes[kvp.Key] = MemberTypeInfo.IEnumerable; //_preMTInfos[kvp.Key] = MemberTypeInfo.IEnumerable;
                }
            }

            if (_Interfaces.ContainsKey(t))
            {
                //Set members type dictionary
                foreach (KeyValuePair<MemberInfo, MemberTypeInfo> kvp in preMTInfos)
                {
                    if (_Interfaces[t].Contains(kvp.Key.Name))
                    {
                        changes[kvp.Key] = changes[kvp.Key] | MemberTypeInfo.Interface;
                        RemoveMember(kvp.Key, pInfos, fInfos);
                    }

                    Type mType = GetMemberType(kvp.Key);
                    if (typeof(IEnumerable).IsAssignableFrom(mType) && !typeof(string).IsAssignableFrom(mType))
                    {
                        changes[kvp.Key] = changes[kvp.Key] | MemberTypeInfo.IEnumerable; //_preMTInfos[kvp.Key] = _preMTInfos[kvp.Key] | MemberTypeInfo.IEnumerable;
                        RemoveMember(kvp.Key, pInfos, fInfos);
                    }
                }
            }
            //Lock-static dictionaries
            lock (_LockObject)
            {
                if (!_mtInfos.ContainsKey(t)) _mtInfos.Add(t, changes);
                else _mtInfos[t] = changes;

                if (!_NamesList.ContainsKey(t)) _NamesList.Add(t, preNamesList);
                else _NamesList[t] = preNamesList;

                if (!_QBPropertyInfos.ContainsKey(t)) _QBPropertyInfos.Add(t, pInfos.ToArray());
                else _QBPropertyInfos[t] = pInfos.ToArray();

                if (!_QBFieldInfos.ContainsKey(t)) _QBFieldInfos.Add(t, fInfos.ToArray());
                else _QBFieldInfos[t] = fInfos.ToArray();

                string columns = "";
                var orderedMembersNames = _QBPropertyInfos[t].Select(x => x.Name)
                    .Union(_QBFieldInfos[t].Select(x => QBuilder.StringSQLBuilder.RemoveFieldsUnderscore(x.Name)))
                    .OrderBy(x => x);
                columns = string.Join(",", orderedMembersNames);

                if (!_Columns.ContainsKey(t)) _Columns.Add(t, columns);
                else _Columns[t] = columns;
            }
        }
        #endregion

        #region public methods
        public void EndConfig<T>()
        {
            StoreType(typeof(T));
            SetMembersInformation(typeof(T));
        }
        /// <summary>
        /// NO overwrite
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instanceConstructor"></param>
        /// <returns></returns>
        public MapperConfig AddConstructor<T>(Func<dynamic, T> instanceConstructor)
        {
            if (instanceConstructor.GetMethodInfo().ReturnType != typeof(T))
                throw new CustomException_MapperConfig($@"MapperConfig.AddConstructor parameter exception:
Instance constructor delegate doesn't return correct type.
Correct type: {typeof(T).ToString()}");

            Type destination = typeof(T);
            if (!_Constructors.ContainsKey(destination))
            {
                lock (_LockObject)
                {
                    if (!_Constructors.ContainsKey(destination))
                    {
                        _Constructors.Add(destination, instanceConstructor);
                        return this;
                    }
                    else throw new CustomException_MapperConfig($@"MapperConfig.AddConstructor.
Dictionary of constructor already have a constructor for that type.
Type: {typeof(T).ToString()}");
                }
            }
            else throw new CustomException_MapperConfig($@"MapperConfig.AddConstructor.
Dictionary of constructor already have a constructor for that type.
Type: {typeof(T).ToString()}");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TMember"></typeparam>
        /// <param name="memberExpression"></param>
        /// <param name="creatorExpression"></param>
        /// <param name="overwrite"></param>
        /// <returns></returns>
        public MapperConfig AddMemberCreator<T, TMember>(
            Expression<Func<T, TMember>> memberExpression,
            Func<dynamic, object> creatorExpression,
            bool overwrite = false)
        {
            string memberName = GetPropertyReturnName(memberExpression);
            return AddMemberCreator<T>(memberName, creatorExpression, overwrite);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="memberName"></param>
        /// <param name="creatorExpression"></param>
        /// <param name="overwrite"></param>
        /// <returns></returns>
        public MapperConfig AddMemberCreator<T>(string memberName, Func<dynamic, object> creatorExpression, bool overwrite = false)
        {
            Type destination = typeof(T);

            if (_NestedProperties.ContainsKey(destination) && _NestedProperties[destination].Contains(memberName))
                throw new CustomException_MapperConfig(
                    $@"MapperConfig.AddMemberCreator: One member({memberName}) can not have a creator expression AND be be setted as a nested type at same type");
            else if (_Interfaces.ContainsKey(destination) && _Interfaces[destination].Contains(memberName))
                throw new CustomException_MapperConfig(
                    $@"MapperConfig.AddMemberCreator: One member({memberName}) can not have a creator expression AND be setted as an interface at same type");

            lock (_LockObject)
            {
                if (!_MembersCreators.ContainsKey(destination))
                {
                    _MembersCreators.Add(destination, new Dictionary<string, Delegate>() { { memberName, creatorExpression } });
                    return this;
                }
                else
                {
                    if (!_MembersCreators[destination].ContainsKey(memberName))
                    {
                        _MembersCreators[destination].Add(memberName, creatorExpression);
                        return this;
                    }
                    else if (!overwrite) return this;
                    else
                    {
                        _MembersCreators[destination][memberName] = creatorExpression;
                        return this;
                    }
                }
            }
        }
        /// <summary>
        /// NO overwrite
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TMember"></typeparam>
        /// <param name="memberExpression"></param>
        /// <returns></returns>
        public MapperConfig AddNestedProperty<T, TMember>(bool isAnInterface, Expression<Func<T, TMember>> memberExpression)
        {
            string propName = GetPropertyReturnName(memberExpression);
            return AddNestedProperty<T>(isAnInterface, propName);
        }
        /// <summary>
        /// NO overwrite
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TMember"></typeparam>
        /// <param name="memberExpressions"></param>
        /// <returns></returns>
        public MapperConfig AddNestedProperty<T, TMember>(bool isAnInterface, params Expression<Func<T, TMember>>[] memberExpressions)
        {
            foreach (Expression<Func<T, TMember>> mExp in memberExpressions)
            {
                string propName = GetPropertyReturnName(mExp);
                AddNestedProperty<T>(isAnInterface, propName);
            }
            return this;
        }
        /// <summary>
        /// NO overwrite
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="memberName"></param>
        /// <returns></returns>
        public MapperConfig AddNestedProperty<T>(bool isAnInterface, string memberName)
        {
            Type destination = typeof(T);

            if (_MembersCreators.ContainsKey(destination) && _MembersCreators[destination].ContainsKey(memberName))
                throw new CustomException_MapperConfig(
                    $@"MapperConfig.AddNestedProperty: One member({memberName}) can not have a creator expression AND be setted as a nested type at same type");

            lock (_LockObject)
            {
                if (isAnInterface)
                {
                    if (!_Interfaces.ContainsKey(destination))
                        _Interfaces.Add(destination, new List<string>() { memberName });
                    else if (!_Interfaces[destination].Contains(memberName))
                        _Interfaces[destination].Add(memberName);
                }

                if (!_NestedProperties.ContainsKey(destination))
                {
                    _NestedProperties.Add(destination, new List<string>() { memberName });
                    return this;
                }
                else
                {
                    if (!_NestedProperties[destination].Contains(memberName))
                        _NestedProperties[destination].Add(memberName);
                    return this;
                }
            }
        }
        /// <summary>
        /// NO overwrite
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="memberNames"></param>
        /// <returns></returns>
        public MapperConfig AddNestedProperty<T>(bool isAnInterface, params string[] memberNames)
        {
            foreach (string mName in memberNames) AddNestedProperty<T>(isAnInterface, mName);
            return this;
        }
        /// <summary>
        /// keyValueDynamicNames: [0] = key name ; [1] = value name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TMember"></typeparam>
        /// <param name="memberExpression"></param>
        /// <param name="keyValueDynamicNames"></param>
        /// <param name="overwrite"></param>
        /// <returns></returns>
        public MapperConfig AddDictionary<T, TMember>(
            Expression<Func<T, TMember>> memberExpression,
            string[] keyValueDynamicNames,
            bool overwrite = false)
        {
            string memberName = GetPropertyReturnName(memberExpression);
            return AddDictionary<T>(memberName, keyValueDynamicNames, overwrite);
        }
        /// <summary>
        /// keyValueDynamicNames: [0] = key name ; [1] = value name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="memberName"></param>
        /// <param name="keyValueDynamicNames"></param>
        /// <returns></returns>
        public MapperConfig AddDictionary<T>(string memberName, string[] keyValueDynamicNames, bool overwrite = false)
        {
            Type destination = typeof(T);

            lock (_LockObject)
            {
                if (!_Dictionaries.ContainsKey(destination))
                {
                    _Dictionaries.Add(destination, new Dictionary<string, string[]>() { { memberName, keyValueDynamicNames } });
                    return this;
                }
                else
                {
                    if (!_Dictionaries[destination].ContainsKey(memberName))
                    {
                        _Dictionaries[destination].Add(memberName, keyValueDynamicNames);
                        return this;
                    }
                    else if (!overwrite) return this;
                    else
                    {
                        _Dictionaries[destination][memberName] = keyValueDynamicNames;
                        return this;
                    }
                }
            }
        }
        public MapperConfig AddInterfacToObjectCondition<TInterface>(Func<dynamic, bool> condition, Type type, bool overwrite = false)
        {
            if (!typeof(TInterface).IsInterface)
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddInterfacesToClassesConditions: TInterface have to be an interface.");

            Type destination = typeof(TInterface);

            lock (_LockObject)
            {
                if (!_InterfacesToObjects.ContainsKey(destination))
                {
                    _InterfacesToObjects.Add(destination, new Dictionary<Type, Func<dynamic, bool>>() { { type, condition } });
                    return this;
                }
                else
                {
                    if (!_InterfacesToObjects[destination].ContainsKey(type))
                    {
                        _InterfacesToObjects[destination].Add(type, condition);
                        return this;
                    }
                    else if (!overwrite) return this;
                    else
                    {
                        _InterfacesToObjects[destination][type] = condition;
                        return this;
                    }
                }
            }
        }
        public MapperConfig AllowDuplicatesIfEnumerable<T, TMember>(Expression<Func<T, TMember>> memberExpression, bool allowDuplicates = false)
        {
            string memberName = GetPropertyReturnName(memberExpression);
            return AllowDuplicatesIfEnumerable<T>(memberName, allowDuplicates);
        }
        public MapperConfig AllowDuplicatesIfEnumerable<T>(string memberName, bool allowDuplicates = false)
        {
            Type destination = typeof(T);
            if (!allowDuplicates) return this;

            lock (_LockObject)
            {
                if (!_AllowDuplicates.ContainsKey(destination))
                {
                    _AllowDuplicates.Add(destination, new List<string>() { memberName });
                    return this;
                }
                else
                {
                    if (!_AllowDuplicates[destination].Contains(memberName))
                        _AllowDuplicates[destination].Add(memberName);
                    return this;
                }
            }
        }
        /// <summary>
        /// Union.
        /// Exclusive: Only dapper results with this prefix will be taken to map the object, even if their names are equal to the type's originals.
        /// F.I. if two objects are retrieved in a same dynamic and both have distinct properties 
        /// called "Id", one of both should add an exclusive prefix or postfix, otherwise the mapper won't know what member
        /// will be for one object and what for the other. That means that when an exclusive pre-postfix are added, ALL corresponding dynamic
        /// members HAVE to use it. Therefore exclusive prefixes will have an overwrite effect, the rest of prefixes will be deleted permanently, 
        /// even the previous exclusive. Same with postfixes.
        /// If after added a pre_postfix as exclusive, the method is called again to add other non-exclusive ones,
        /// it will convert the old exclusive to non-exclusive and add the new ones.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefixes"></param>
        /// <returns></returns>
        public MapperConfig AddPrefixes<T>(string[] prefixes, bool exclusive = false)
        {
            Type destination = typeof(T);
            lock (_LockObject)
            {
                if (!exclusive)
                {
                    if (!_Prefixes[destination].Item2)
                        _Prefixes[destination] = new Tuple<string[], bool>(_Prefixes[destination].Item1, false);

                    if (!_Prefixes.ContainsKey(destination))
                    {
                        _Prefixes.Add(destination, new Tuple<string[], bool>(prefixes, false));
                        return this;
                    }
                    else
                    {
                        _Prefixes[destination] = new Tuple<string[], bool>(
                            _Prefixes[destination].Item1.Union(prefixes).Distinct().ToArray(),
                            false);
                        return this;
                    }
                }
                else
                {
                    _Prefixes[destination] = new Tuple<string[], bool>(prefixes, true);
                    return this;
                }
            }
        }
        public MapperConfig RemovePrefixes<T>(string[] prefixes)
        {
            Type destination = typeof(T);

            if (!_Prefixes.ContainsKey(destination))
                throw new CustomException_MapperConfig(
                    $@"Mapperconfig.RemovePrefixes There're no prefixes for type {destination.ToString()}.");
            else
            {
                lock (_LockObject)
                {
                    if (!_Prefixes.ContainsKey(destination))
                        throw new CustomException_MapperConfig(
                            $@"Mapperconfig.RemovePrefixes There're no prefixes for type {destination.ToString()}.");
                    else
                    {
                        _Prefixes[destination] = new Tuple<string[], bool>(
                            _Prefixes[destination].Item1.Where(pref => !prefixes.Contains(pref)).ToArray(),
                            _Prefixes[destination].Item2);
                        return this;
                    }
                }
            }
        }
        /// <summary>
        /// Union.
        /// Exclusive: Only dapper results with this prefix will be taken to map the object, even if their names are equal to the type's originals.
        /// F.I. if two objects are retrieved in a same dynamic and both have distinct properties 
        /// called "Id", one of both should add an exclusive prefix or postfix, otherwise the mapper won't know what member
        /// will be for one object and what for the other. That means that when an exclusive pre-postfix are added, ALL corresponding dynamic
        /// members HAVE to use it. Therefore exclusive prefixes will have an overwrite effect, the rest of prefixes will be deleted permanently, 
        /// even the previous exclusive. Same with postfixes.
        /// If after added a pre_postfix as exclusive, the method is called again to add other non-exclusive ones,
        /// it will convert the old exclusive to non-exclusive and add the new ones.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefixes"></param>
        /// <returns></returns>
        public MapperConfig AddPostfixes<T>(string[] postfixes, bool exclusive = false)
        {
            Type destination = typeof(T);
            lock (_LockObject)
            {
                if (!exclusive)
                {
                    if (!_Postfixes[destination].Item2)
                        _Postfixes[destination] = new Tuple<string[], bool>(_Postfixes[destination].Item1, false);

                    if (!_Postfixes.ContainsKey(destination))
                    {
                        _Postfixes.Add(destination, new Tuple<string[], bool>(postfixes, false));
                        return this;
                    }
                    else
                    {
                        _Postfixes[destination] = new Tuple<string[], bool>(
                            _Postfixes[destination].Item1.Union(postfixes).Distinct().ToArray(),
                            false);
                        return this;
                    }
                }
                else
                {
                    _Postfixes[destination] = new Tuple<string[], bool>(postfixes, true);
                    return this;
                }
            }
        }
        public MapperConfig RemovePostfixes<T>(string[] postfixes)
        {
            Type destination = typeof(T);

            if (!_Postfixes.ContainsKey(destination))
                throw new CustomException_MapperConfig(
                    $@"Mapperconfig.RemovePrefixes There're no prefixes for type {destination.ToString()}.");
            else
            {
                lock (_LockObject)
                {
                    if (!_Postfixes.ContainsKey(destination))
                        throw new CustomException_MapperConfig(
                            $@"Mapperconfig.RemovePrefixes There're no prefixes for type {destination.ToString()}.");
                    else
                    {
                        _Postfixes[destination] = new Tuple<string[], bool>(
                            _Postfixes[destination].Item1.Where(pref => !postfixes.Contains(pref)).ToArray(),
                            _Postfixes[destination].Item2);
                        return this;
                    }
                }
            }
        }
        #endregion
    }
}
