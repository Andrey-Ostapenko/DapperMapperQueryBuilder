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
        private void RemoveStaticDictionaryKey<R>(Dictionary<Type, R> dict, Type key)
        {
            if (dict.ContainsKey(key))
            {
                lock (_LockObject)
                {
                    if (dict.ContainsKey(key))
                        dict.Remove(key);
                }
            }
        }
        private void RemoveAllConfigExceptConstructor(Type t)
        {
            /*
        protected static Dictionary<Type, Dictionary<string, Delegate>> _MembersCreators;
        protected static Dictionary<Type, List<string>> _NestedProperties;
        protected static Dictionary<Type, List<string>> _Interfaces;
        protected static Dictionary<Type, Tuple<string[], bool>> _Prefixes;
        protected static Dictionary<Type, Tuple<string[], bool>> _Postfixes;
        protected static Dictionary<Type, Dictionary<string, string[]>> _Dictionaries;
        protected static Dictionary<Type, Dictionary<Type, Func<dynamic, bool>>> _InterfacesToObjects;
        protected static Dictionary<Type, List<string>> _AllowDuplicates;
            */
            RemoveStaticDictionaryKey(_MembersCreators, t);
            RemoveStaticDictionaryKey(_NestedProperties, t);
            RemoveStaticDictionaryKey(_Interfaces, t);
            //RemoveStaticDictionaryKey(_Prefixes, t);
            //RemoveStaticDictionaryKey(_Postfixes, t);
            RemoveStaticDictionaryKey(_Dictionaries, t);
            RemoveStaticDictionaryKey(_InterfacesToObjects, t);
            RemoveStaticDictionaryKey(_AllowDuplicates, t);
        }
        private void SetMembersInformation(Type t)
        {
            if (_OnlyConstructor.Contains(t))
                RemoveAllConfigExceptConstructor(t);

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
                .Select(x =>
                {
                    if (_Ignores.ContainsKey(t) && _Ignores[t].Contains(x.Name))
                        return new KeyValuePair<MemberInfo, MemberTypeInfo>(x, MemberTypeInfo.Ignore);
                    else return new KeyValuePair<MemberInfo, MemberTypeInfo>(x, MemberTypeInfo.BuiltIn);
                })
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
                foreach (KeyValuePair<MemberInfo, MemberTypeInfo> kvp in preMTInfos.Where(kvp=>kvp.Value != MemberTypeInfo.Ignore))
                {
                    if (_MembersCreators[t].ContainsKey(kvp.Key.Name))
                    {
                        if (!_OnlyConstructor.Contains(t)) changes[kvp.Key] = MemberTypeInfo.Creator;//_preMTInfos[kvp.Key] = MemberTypeInfo.Creator;
                        RemoveMember(kvp.Key, pInfos, fInfos);
                    }
                    else
                    {
                        if (_NestedProperties[t].Contains(kvp.Key.Name))
                        {
                            if (!_OnlyConstructor.Contains(t)) changes[kvp.Key] = MemberTypeInfo.Nested;//_preMTInfos[kvp.Key] = MemberTypeInfo.Nested;
                            RemoveMember(kvp.Key, pInfos, fInfos);
                        }

                        Type mType = GetMemberType(kvp.Key);
                        if (typeof(IEnumerable).IsAssignableFrom(mType) && !typeof(string).IsAssignableFrom(mType))
                        {
                            if (!_OnlyConstructor.Contains(t)) changes[kvp.Key] = changes[kvp.Key] | MemberTypeInfo.IEnumerable; //_preMTInfos[kvp.Key] = _preMTInfos[kvp.Key] | MemberTypeInfo.IEnumerable;
                            RemoveMember(kvp.Key, pInfos, fInfos);
                        }
                    }
                }
            }
            else if (_MembersCreators.ContainsKey(t))
            {
                //Set members type dictionary
                foreach (KeyValuePair<MemberInfo, MemberTypeInfo> kvp in preMTInfos.Where(kvp => kvp.Value != MemberTypeInfo.Ignore))
                {
                    if (_MembersCreators[t].ContainsKey(kvp.Key.Name))
                    {
                        if (!_OnlyConstructor.Contains(t)) changes[kvp.Key] = MemberTypeInfo.Creator; //_preMTInfos[kvp.Key] = MemberTypeInfo.Creator;
                        RemoveMember(kvp.Key, pInfos, fInfos);
                    }
                    else
                    {
                        Type mType = GetMemberType(kvp.Key);
                        if (typeof(IEnumerable).IsAssignableFrom(mType) && !typeof(string).IsAssignableFrom(mType))
                        {
                            if (!_OnlyConstructor.Contains(t)) changes[kvp.Key] = changes[kvp.Key] | MemberTypeInfo.IEnumerable; //_preMTInfos[kvp.Key] = _preMTInfos[kvp.Key] | MemberTypeInfo.IEnumerable;
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
                foreach (KeyValuePair<MemberInfo, MemberTypeInfo> kvp in preMTInfos.Where(kvp => kvp.Value != MemberTypeInfo.Ignore))
                {
                    if (_NestedProperties[t].Contains(kvp.Key.Name))
                    {
                        if (!_OnlyConstructor.Contains(t)) changes[kvp.Key] = MemberTypeInfo.Nested; //_preMTInfos[kvp.Key] = MemberTypeInfo.Nested;
                        RemoveMember(kvp.Key, pInfos, fInfos);
                    }

                    Type mType = GetMemberType(kvp.Key);
                    if (typeof(IEnumerable).IsAssignableFrom(mType) && !typeof(string).IsAssignableFrom(mType))
                    {
                        if (!_OnlyConstructor.Contains(t)) changes[kvp.Key] = changes[kvp.Key] | MemberTypeInfo.IEnumerable; //_preMTInfos[kvp.Key] = _preMTInfos[kvp.Key] | MemberTypeInfo.IEnumerable;
                        RemoveMember(kvp.Key, pInfos, fInfos);
                    }
                }
            }
            //built-in
            else if (!_OnlyConstructor.Contains(t))
            {
                //Set members type dictionary
                foreach (KeyValuePair<MemberInfo, MemberTypeInfo> kvp in preMTInfos.Where(kvp => kvp.Value != MemberTypeInfo.Ignore))
                {
                    Type mType = GetMemberType(kvp.Key);
                    if (typeof(IEnumerable).IsAssignableFrom(mType) && !typeof(string).IsAssignableFrom(mType))
                        changes[kvp.Key] = MemberTypeInfo.IEnumerable; //_preMTInfos[kvp.Key] = MemberTypeInfo.IEnumerable;
                }
            }

            if (_Interfaces.ContainsKey(t))
            {
                //Set members type dictionary
                foreach (KeyValuePair<MemberInfo, MemberTypeInfo> kvp in preMTInfos.Where(kvp => kvp.Value != MemberTypeInfo.Ignore))
                {
                    if (_Interfaces[t].Contains(kvp.Key.Name))
                    {
                        if (!_OnlyConstructor.Contains(t)) changes[kvp.Key] = changes[kvp.Key] | MemberTypeInfo.Interface;
                        RemoveMember(kvp.Key, pInfos, fInfos);
                    }

                    Type mType = GetMemberType(kvp.Key);
                    if (typeof(IEnumerable).IsAssignableFrom(mType) && !typeof(string).IsAssignableFrom(mType))
                    {
                        if (!_OnlyConstructor.Contains(t)) changes[kvp.Key] = changes[kvp.Key] | MemberTypeInfo.IEnumerable; //_preMTInfos[kvp.Key] = _preMTInfos[kvp.Key] | MemberTypeInfo.IEnumerable;
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
        /// <summary>
        /// Always call this when you have finished an object configuration, even if you just want the object to be included (f.i. even if the object
        /// have no nested types, only built-in, you have to call this method).
        /// Only case you musn't call this is if you added prefixes or postfixes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void EndConfig<T>()
        {
            StoreType(typeof(T));
            SetMembersInformation(typeof(T));
        }
        /// <summary>
        /// Mapper will use the Func as a constructor to create T type objects instances and then fill that instances.
        /// Only one constructor per type. NO overwrite.
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
            if (destination.IsInterface)
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddConstructor: Interfaces can't have a constructor!");

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
Already have a constructor for that type.
Type: {typeof(T).ToString()}");
                }
            }
            else throw new CustomException_MapperConfig($@"MapperConfig.AddConstructor.
Already have a constructor for that type.
Type: {typeof(T).ToString()}");
        }
        /// <summary>
        /// Mapper will use the Func to fully create the object, no constructors, nested members, etc., only the Func.
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
            if (_OnlyConstructor.Contains(typeof(T)))
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddMemberCreator: You can't any configuration if MapOnlyConstructor have been setted.");
            string memberName = GetPropertyReturnName(memberExpression);
            return AddMemberCreator<T>(memberName, creatorExpression, overwrite);
        }
        /// <summary>
        /// Mapper will use the Func to fully create the object, no constructors, nested members, etc., only the Func.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="memberName"></param>
        /// <param name="creatorExpression"></param>
        /// <param name="overwrite"></param>
        /// <returns></returns>
        public MapperConfig AddMemberCreator<T>(string memberName, Func<dynamic, object> creatorExpression, bool overwrite = false)
        {
            Type destination = typeof(T);
            if (_OnlyConstructor.Contains(destination))
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddMemberCreator: You can't any configuration if MapOnlyConstructor have been setted.");
            if (_NestedProperties.ContainsKey(destination) && _NestedProperties[destination].Contains(memberName))
                throw new CustomException_MapperConfig(
                    $@"MapperConfig.AddMemberCreator: One member({memberName}) can not have a creator expression AND be setted as a nested type at same type");
            //else if (_Interfaces.ContainsKey(destination) && _Interfaces[destination].Contains(memberName))
            //    throw new CustomException_MapperConfig(
            //        $@"MapperConfig.AddMemberCreator: One member({memberName}) can not have a creator expression AND be setted as an interface at same type");
            if (_Ignores.ContainsKey(destination) && _Ignores[destination].Contains(memberName))
                throw new CustomException_MapperConfig(
                    $@"MapperConfig.AddMemberCreator: One member({memberName}) can not have a creator expression if it was setted for ignore.");

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
        /// Set member as nested type so mapper will try to find a mapper of the nested type to create that member. Obviously you have to 
        /// config the nested type mapper apart. NO overwrite
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TMember"></typeparam>
        /// <param name="memberExpression"></param>
        /// <returns></returns>
        public MapperConfig AddNestedProperty<T, TMember>(bool isAnInterface, Expression<Func<T, TMember>> memberExpression)
        {
            if (_OnlyConstructor.Contains(typeof(T)))
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddNestedProperty: You can't any configuration if MapOnlyConstructor have been setted.");
            string propName = GetPropertyReturnName(memberExpression);
            return AddNestedProperty<T>(isAnInterface, propName);
        }
        /// <summary>
        /// Set members (of a same type) as nested type so mapper will try to find a mapper of the nested type to create that member. Obviously you have to 
        /// config the nested type mapper apart. NO overwrite
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TMember"></typeparam>
        /// <param name="memberExpressions"></param>
        /// <returns></returns>
        public MapperConfig AddNestedProperty<T, TMember>(bool isAnInterface, params Expression<Func<T, TMember>>[] memberExpressions)
        {
            if (_OnlyConstructor.Contains(typeof(T)))
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddNestedProperty: You can't any configuration if MapOnlyConstructor have been setted.");
            foreach (Expression<Func<T, TMember>> mExp in memberExpressions)
            {
                string propName = GetPropertyReturnName(mExp);
                AddNestedProperty<T>(isAnInterface, propName);
            }
            return this;
        }
        /// <summary>
        /// Set member as nested type so mapper will try to find a mapper of the nested type to create that member. Obviously you have to 
        /// config the nested type mapper apart. NO overwrite
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="memberName"></param>
        /// <returns></returns>
        public MapperConfig AddNestedProperty<T>(bool isAnInterface, string memberName)
        {
            Type destination = typeof(T);

            if (_OnlyConstructor.Contains(destination))
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddNestedProperty: You can't any configuration if MapOnlyConstructor have been setted.");
            if (_MembersCreators.ContainsKey(destination) && _MembersCreators[destination].ContainsKey(memberName))
                throw new CustomException_MapperConfig(
                    $@"MapperConfig.AddNestedProperty: One member({memberName}) can not have a creator expression AND be setted as a nested type at same type");
            if (_Ignores.ContainsKey(destination) && _Ignores[destination].Contains(memberName))
                throw new CustomException_MapperConfig(
                    $@"MapperConfig.AddNestedProperty: One member({memberName}) can not be setted as nested property if it was setted for ignore.");

            if (isAnInterface)
            {
                lock (_LockObject)
                {
                    if (!_Interfaces.ContainsKey(destination))
                        _Interfaces.Add(destination, new List<string>() { memberName });
                    else if (!_Interfaces[destination].Contains(memberName))
                        _Interfaces[destination].Add(memberName);
                }
            }

            lock (_LockObject)
            {
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
        /// Set members (of a same type) as nested type so mapper will try to find a mapper of the nested type to create that member. Obviously you have to 
        /// config the nested type mapper apart. NO overwrite
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="memberNames"></param>
        /// <returns></returns>
        public MapperConfig AddNestedProperty<T>(bool isAnInterface, params string[] memberNames)
        {
            if (_OnlyConstructor.Contains(typeof(T)))
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddNestedProperty: You can't any configuration if MapOnlyConstructor have been setted.");
            foreach (string mName in memberNames) AddNestedProperty<T>(isAnInterface, mName);
            return this;
        }
        /// <summary>
        /// Mapper will ignore the property specified. Can't be used with properties previously configurated with AddMemberCreator, 
        /// AddNestedProperty or AddDictionary.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TMember"></typeparam>
        /// <param name="memberExpression"></param>
        /// <returns></returns>
        public MapperConfig AddIgnoreProperty<T, TMember>(Expression<Func<T, TMember>> memberExpression)
        {
            if (_OnlyConstructor.Contains(typeof(T)))
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddIgnoreProperty: You can't any configuration if MapOnlyConstructor have been setted.");
            string memberName = GetPropertyReturnName(memberExpression);
            return AddIgnoreProperty<T>(memberName);
        }
        /// <summary>
        /// Mapper will ignore the property specified. Can't be used with properties previously configurated with AddMemberCreator, 
        /// AddNestedProperty or AddDictionary.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="memberName"></param>
        /// <returns></returns>
        public MapperConfig AddIgnoreProperty<T>(string memberName)
        {
            Type destination = typeof(T);
            if (_OnlyConstructor.Contains(destination))
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddIgnoreProperty: You can't any configuration if MapOnlyConstructor have been setted.");
            if ((_MembersCreators.ContainsKey(destination) && _MembersCreators[destination].ContainsKey(memberName)) || //member have a creator
                (_NestedProperties.ContainsKey(destination) && _NestedProperties[destination].Contains(memberName)) || //member setted as nested
                (_Dictionaries.ContainsKey(destination) && _Dictionaries[destination].ContainsKey(memberName))) //member setted as dictionary
                    throw new CustomException_MapperConfig(
                        $@"MapperConfig.AddIgnoreProperty: Can't ignore a member({memberName}) if parameter overwriteOtherConfigurations is false and
the member have been configurated before with a member creator, as nested or as dictionary.");

            lock (_LockObject)
            {
                if (!_Ignores.ContainsKey(destination))
                {
                    _Ignores.Add(destination, new List<string>() { memberName });
                    return this;
                }
                else
                {
                    if (!_Ignores[destination].Contains(memberName))
                        _Ignores[destination].Add(memberName);
                    return this;
                }
            }
        }
        /// <summary>
        /// Set a member as a dictionary and the name that keys and values will have in the Dapper's dynamic result:
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
            if (_OnlyConstructor.Contains(typeof(T)))
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddDictionary: You can't any configuration if MapOnlyConstructor have been setted.");
            string memberName = GetPropertyReturnName(memberExpression);
            return AddDictionary<T>(memberName, keyValueDynamicNames, overwrite);
        }
        /// <summary>
        /// Set a member as a dictionary and the name that keys and values will have in the Dapper's dynamic result:
        /// keyValueDynamicNames: [0] = key name ; [1] = value name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="memberName"></param>
        /// <param name="keyValueDynamicNames"></param>
        /// <returns></returns>
        public MapperConfig AddDictionary<T>(string memberName, string[] keyValueDynamicNames, bool overwrite = false)
        {
            Type destination = typeof(T);
            if (_OnlyConstructor.Contains(destination))
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddDictionary: You can't any configuration if MapOnlyConstructor have been setted.");
            if (_Ignores.ContainsKey(destination) && _Ignores[destination].Contains(memberName))
                throw new CustomException_MapperConfig(
                    $@"MapperConfig.AddDictionary: One member({memberName}) can not be setted as a dictionary if it was setted for ignore.");

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
        /// <summary>
        /// If a member is setted as interface (with AddNestedProperty(true,...)), the mapper will search through conditions setted with this method
        /// using the Func, and map an object of type "type" instead.
        /// F.i. if you have "interface iPerson", "class Customer : iPerson" and use 
        /// AddInterfaceToObjectCondition<iPerson>(FuncCondition, typeof(Customer)), the mapper of "iPerson" will create "Customer" objects when
        /// the condition will be satisfied.
        /// </summary>
        /// <typeparam name="TInterface"></typeparam>
        /// <param name="condition"></param>
        /// <param name="type"></param>
        /// <param name="overwrite"></param>
        /// <returns></returns>
        public MapperConfig AddInterfaceToObjectCondition<TInterface>(Func<dynamic, bool> condition, Type type, bool overwrite = false)
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
        /// <summary>
        /// By default duplicates created with JOINs are removed, you can use this to allow duplicates. With allowDuplicates = false is the same as
        /// if you don't call this method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TMember"></typeparam>
        /// <param name="memberExpression"></param>
        /// <param name="allowDuplicates"></param>
        /// <returns></returns>
        public MapperConfig AllowDuplicatesIfEnumerable<T, TMember>(Expression<Func<T, TMember>> memberExpression, bool allowDuplicates = false)
        {
            string memberName = GetPropertyReturnName(memberExpression);
            return AllowDuplicatesIfEnumerable<T>(memberName, allowDuplicates);
        }
        /// <summary>
        /// By default duplicates created with JOINs are removed, you can use this to allow duplicates. With allowDuplicates = false is the same as
        /// if you don't call this method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="memberName"></param>
        /// <param name="allowDuplicates"></param>
        /// <returns></returns>
        public MapperConfig AllowDuplicatesIfEnumerable<T>(string memberName, bool allowDuplicates = false)
        {
            Type destination = typeof(T);
            if (_Ignores.ContainsKey(destination) && _Ignores[destination].Contains(memberName))
                throw new CustomException_MapperConfig(
                    $@"MapperConfig.AllowDuplicatesIfEnumerable: One member({memberName}) can not be setted for allow duplicates if it was setted for ignore.");

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
        /// If called, the mapper won't map any specific member of the T object, the mapper will only create an instance, with the constructor provided 
        /// by MapperConfig.AddConstructor() IF it was provided, and return that instance, no more.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public MapperConfig MapOnlyConstructor<T>()
        {
            Type destination = typeof(T);
            if (destination.IsInterface)
                throw new CustomException_MapperConfig(
                    @"MapperConfig.MapOnlyConstructor: Interfaces can't have a constructor!");

            if (!_OnlyConstructor.Contains(destination))
            {
                lock (_LockObject)
                {
                    if (!_OnlyConstructor.Contains(destination))
                        _OnlyConstructor.Add(destination);
                }
            }

            return this;
        }
        /// <summary>
        /// Prefixes that Dapper's dynamic result will use in the member names.
        /// With exclusive = false prefixes don't overwrite but get added to an array of possible prefixes.
        /// With exclusive = true only dapper results with this prefix will be taken to map the object, even if their names are equal to the type's originals.
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
            if (_OnlyConstructor.Contains(destination))
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddPrefixes: You can't any configuration if MapOnlyConstructor have been setted.");

            lock (_LockObject)
            {
                if (!exclusive)
                {
                    if (_Prefixes.ContainsKey(destination) && !_Prefixes[destination].Item2)
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
        /// <summary>
        /// Remove specified prefixes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefixes"></param>
        /// <returns></returns>
        public MapperConfig RemovePrefixes<T>(string[] prefixes)
        {
            Type destination = typeof(T);

            if (!_Prefixes.ContainsKey(destination))
                throw new CustomException_MapperConfig(
                    $@"Mapperconfig.RemovePrefixes: There're no prefixes for type {destination.ToString()}.");
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
        /// Postfixes that Dapper's dynamic result will use in the member names.
        /// With exclusive = false postfixes don't overwrite but get added to an array of possible postfixes.
        /// With exclusive = true only dapper results with this postfix will be taken to map the object, even if their names are equal to the type's originals.
        /// F.I. if two objects are retrieved in a same dynamic and both have distinct properties 
        /// called "Id", one of both should add an exclusive prefix or postfix, otherwise the mapper won't know what member
        /// will be for one object and what for the other. That means that when an exclusive pre-postfix are added, ALL corresponding dynamic
        /// members HAVE to use it. Therefore exclusive postfixes will have an overwrite effect, the rest of postfixes will be deleted permanently, 
        /// even the previous exclusive. Same with prefixes.
        /// If after added a pre_postfix as exclusive, the method is called again to add other non-exclusive ones,
        /// it will convert the old exclusive to non-exclusive and add the new ones.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefixes"></param>
        /// <returns></returns>
        public MapperConfig AddPostfixes<T>(string[] postfixes, bool exclusive = false)
        {
            Type destination = typeof(T);
            if (_OnlyConstructor.Contains(destination))
                throw new CustomException_MapperConfig(
                    @"MapperConfig.AddPostfixes: You can't any configuration if MapOnlyConstructor have been setted.");

            lock (_LockObject)
            {
                if (!exclusive)
                {
                    if (_Postfixes.ContainsKey(destination) && !_Postfixes[destination].Item2)
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
        /// <summary>
        /// Remove specified postfixes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="postfixes"></param>
        /// <returns></returns>
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
