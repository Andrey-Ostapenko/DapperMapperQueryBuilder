using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;
using MQBStatic;
using Exceptions;

namespace Mapper
{
    [Flags]
    public enum MemberTypeInfo { BuiltIn = 1, Nested = 2, Creator = 4, IEnumerable = 8, Dictionary = 16, Interface = 32, Ignore = 64 }
    
    public interface iDapperMapper
    {
        Type TType { get; }
        IEnumerable<string> NamesList { get; }
        Tuple<string[], bool> Prefixes { get; }
        Tuple<string[], bool> Postfixes { get; }

        IEnumerable<dynamic> GetDistinctDapperResult(IEnumerable<dynamic> origDapperResult, bool cleanResult);
        object NoGenericMap(dynamic dapperResult, bool cleanResult = false);
        object NoGenericMap(IEnumerable<dynamic> dapperResult, bool cleanResult = false);
        bool CheckIfDynamicHasAllTypeMembersByName(dynamic dyn);
    }

    public class DapperMapper<T> : DMStatic_Mapper, iDapperMapper
    {
        public DapperMapper(MapperStore store)
        {
            this.TType = typeof(T);
            this.MappersStore = store;
            this.MappersStore.StoreMapper(this.TType, this);
        }

        #region properties
        public MapperStore MappersStore { get; private set; }
        public Dictionary<MemberInfo, MemberTypeInfo> mtInfos { get { return _mtInfos[this.TType]; } }
        public Type TType { get; private set; }
        public IEnumerable<string> NamesList { get { return _NamesList[this.TType]; } }
        public Tuple<string[], bool> Prefixes { get { return _Prefixes.ContainsKey(this.TType) ? _Prefixes[this.TType] : null; } }
        public Tuple<string[], bool> Postfixes { get { return _Postfixes.ContainsKey(this.TType) ? _Postfixes[this.TType] : null; } }
        #endregion

        #region helpers
        private T NewObject(dynamic dyn)
        {
            Type t = typeof(T);
            bool IsInterfaceNotIEnumerable = t.IsInterface 
                && !typeof(IDictionary).IsAssignableFrom(t) 
                && !(typeof(IEnumerable).IsAssignableFrom(t) && !typeof(string).IsAssignableFrom(t));
            if (IsInterfaceNotIEnumerable)
                throw new CustomException_DapperMapper(
                    @"DapperMapper.NewObject: Exception trying to instantiate an interface that isn't an IEnumerable. This is a BUG.");

            T newObj;
            //if there are a constructor configurated
            if (_Constructors.ContainsKey(this.TType))
            {
                //object[] constructorParams = GetConstructorParams();
                try
                {
                    Func<dynamic, T> constDelegate = (Func<dynamic, T>)_Constructors[this.TType];
                    newObj = constDelegate(dyn);
                }
                catch (Exception err)
                {
                    throw new CustomException_DapperMapper(
                        $@"DapperMapper.NewObject: Exception using constructor to create object of type {TType.Name} 
with delegate {_Constructors[TType]}.", err);
                }
            }
            //if there are no constructor configurated, use parameterless constructor
            else newObj = Activator.CreateInstance<T>();

            return newObj;
        }
        #endregion

        #region public methods
        /// <summary>
        /// Remove duplicated results due to JOINs.
        /// </summary>
        /// <param name="origDapperResult"></param>
        /// <param name="cleanResult"></param>
        /// <returns></returns>
        public IEnumerable<dynamic> GetDistinctDapperResult(IEnumerable<dynamic> origDapperResult, bool cleanResult)
        {            
            PrePostFixesParser parser = new PrePostFixesParser(this);
            IEnumerable<string> names = this.NamesList;
            List<dynamic> result = new List<dynamic>();
            
            foreach(dynamic dyn in origDapperResult)
            {
                IDictionary<string, object> dict = 
                    (!cleanResult ? parser.GetTypeMembersWithoutPrePostFixes(dyn, names) : dyn) 
                    as IDictionary<string, object>;

                bool distinct = true;
                foreach(dynamic resultDyn in result)
                {
                    IDictionary<string, object> resDict = resultDyn as IDictionary<string, object>;

                    if(dict.Keys.SequenceEqual(resDict.Keys) && dict.Values.SequenceEqual(resDict.Values))
                    {
                        distinct = false;
                        break;
                    }
                }

                if (distinct) result.Add(dyn);
            }
            return result;
        }
        /// <summary>
        /// Check if the dynamic object have all the members needed to map a new T object, except those setted as IEnumerable,
        /// which should be provided in others dynamic.
        /// </summary>
        /// <param name="dyn"></param>
        /// <returns></returns>
        public bool CheckIfDynamicHasAllTypeMembersByName(dynamic dyn)
        {
            IDictionary<string, object> membersDict = dyn as IDictionary<string, object>;
            IEnumerable<string> dynList = membersDict.Select(kvp => kvp.Key);
            PrePostFixesParser parser = new PrePostFixesParser(this);
            IEnumerable<string> list = parser.GetCleanNamesList(this.NamesList);
            
            return !dynList.Except(list).Any() && !list.Except(dynList).Any();
        }
        /// <summary>
        /// Check if the dynamic object have all the members needed to map a new T object, except those setted as IEnumerable,
        /// which should be provided in others dynamic.
        /// </summary>
        /// <param name="membersDict"></param>
        /// <returns></returns>
        public bool CheckIfDynamicHasAllTypeMembersByName(IDictionary<string, object> membersDict)
        {
            IEnumerable<string> dynList = membersDict.Select(kvp => kvp.Key);
            PrePostFixesParser parser = new PrePostFixesParser(this);

            return dynList.SequenceEqual(parser.GetCleanNamesList(this.NamesList));
        }
        /// <summary>
        /// Generic Map.
        /// </summary>
        /// <param name="dapperResult"></param>
        /// <returns></returns>
        public T Map(IEnumerable<dynamic> dapperResult, bool cleanResult = false)
        {
            var parser = new PrePostFixesParser(this);
            T mapped = this.NewObject(dapperResult.First());
            if (_OnlyConstructor.Contains(this.TType)) return mapped;

            //TODO: divide el siguiente foreach en dos con dos nuevos diccionarios estáticos, uno para pInfos y otro para fInfos, 
            //aunque se repita código: hacer métodos para cada parte del código del tipo:
            //private T PreMapCreator(KeyValuePair<PropertyInfo, MemberTypeInfo> kvp, IEnumerable<dynamic> dapperResult, bool cleanResult = false)
            //private T PreMapIEnumerable(KeyValuePair<PropertyInfo, MemberTypeInfo> kvp, IEnumerable<dynamic> dapperResult, bool cleanResult = false)
            //...
            
            //Loop through all members
            foreach (KeyValuePair<MemberInfo, MemberTypeInfo> kvp in mtInfos)
            {
                if (kvp.Value == MemberTypeInfo.Ignore)
                    continue;
                //Member have a creator
                else if ((kvp.Value & MemberTypeInfo.Creator) == MemberTypeInfo.Creator)
                {
                    //MemberDelegate mDel = (MemberDelegate)_MembersCreators[this.TType][kvp.Key.Name];
                    Func<dynamic, object> mDel = (Func<dynamic, object>)_MembersCreators[this.TType][kvp.Key.Name];

                    if (kvp.Key.MemberType == MemberTypes.Property) ((PropertyInfo)kvp.Key).SetValue(mapped, mDel(dapperResult));
                    else ((FieldInfo)kvp.Key).SetValue(mapped, mDel(dapperResult));
                }
                //Member is IDictionary or IEnumerable
                else if ((kvp.Value & MemberTypeInfo.IEnumerable) == MemberTypeInfo.IEnumerable)
                {
                    Type t = GetMemberType(kvp.Key);
                    //if ((kvp.Value & MemberTypeInfo.Interface) == MemberTypeInfo.Interface) t = ResolveInterface(kvp.Key, dapperResult);
                    //else t = GetMemberType(kvp.Key);
                    /*
                    {
                        //Type of property or field
                        if (kvp.Key.MemberType == MemberTypes.Property) t = ((PropertyInfo)kvp.Key).PropertyType;
                        else t = ((FieldInfo)kvp.Key).FieldType;
                    }*/
                    bool isAnInterface = (kvp.Value & MemberTypeInfo.Interface) == MemberTypeInfo.Interface;
                    bool isNested = (kvp.Value & MemberTypeInfo.Nested) == MemberTypeInfo.Nested;

                    //If member is a dictionary
                    if (typeof(IDictionary).IsAssignableFrom(t))
                    {
                        //Create a dummy dictionary with the dapper's dynamic result which should be equal to the final one
                        DictionaryMapper dictMapper = new DictionaryMapper(dapperResult, kvp.Key.Name, isNested, isAnInterface, cleanResult, t, this);

                        try
                        {
                            if (kvp.Key.MemberType == MemberTypes.Property) ((PropertyInfo)kvp.Key).SetValue(mapped, dictMapper.DummyDictionary);
                            else ((FieldInfo)kvp.Key).SetValue(mapped, dictMapper.DummyDictionary);
                        }
                        catch (Exception err)
                        {
                            throw new CustomException_DapperMapper(
                                $@"DapperMapper.Map: Couldn't map IDictionary member {kvp.Key.Name} with value contained by dynamic object.
Incorrect type of value?: {kvp.Value.ToString()}",
                                err);
                        }
                    }
                    //Rest of enumerables
                    else
                    {
                        IEnumerable<dynamic> iEnumDapperResult;
                        //Select current member's values from dynamic
                        if (isNested && !cleanResult)
                        {
                            //Type mType = t; // GetMemberType(kvp.Key);//IEnumerable<T>
                            Type genericType = t.GenericTypeArguments[0];//mType.GenericTypeArguments[0];//T
                            if ((kvp.Value & MemberTypeInfo.Interface) == MemberTypeInfo.Interface)
                            {
                                bool genericIsInterfaceNotIEnumerable =
                                    genericType.IsInterface &&
                                    !typeof(IDictionary).IsAssignableFrom(genericType) &&
                                    !(typeof(IEnumerable).IsAssignableFrom(genericType) && !typeof(string).IsAssignableFrom(genericType));

                                if (genericIsInterfaceNotIEnumerable) genericType = ResolveInterface(genericType, dapperResult);
                            }

                            iDapperMapper nestedMapper = MappersStore.GetMapper(genericType);
                            var nestedParser = new PrePostFixesParser(nestedMapper);

                            iEnumDapperResult = dapperResult
                                .Select(dyn => nestedParser.GetTypeMembersWithoutPrePostFixes(dyn, nestedMapper.NamesList));
                        }
                        else if (!cleanResult) iEnumDapperResult = dapperResult.Select(dyn => parser.RemovePrePostFixesFromDictionary(dyn));
                        else iEnumDapperResult = dapperResult;

                        //Create dummy IEnumerable
                        EnumerableMapper enumMapper = new EnumerableMapper(iEnumDapperResult, kvp.Key.Name, isNested, t, this.TType); ;
                        var dummy = Activator.CreateInstance(t, enumMapper.DummyEnumerable);

                        try
                        {
                            if (kvp.Key.MemberType == MemberTypes.Property) ((PropertyInfo)kvp.Key).SetValue(mapped, dummy);
                            else ((FieldInfo)kvp.Key).SetValue(mapped, dummy);
                        }
                        catch (Exception err)
                        {
                            throw new CustomException_DapperMapper(
                                $@"DapperMapper.Map: Couldn't map IEnumerable member {kvp.Key.Name} with value contained by dynamic object.
Incorrect type of value?: {kvp.Value.ToString()}",
                                err);
                        }
                    }
                }//End IDictionary/IEnumerable
                //If Built-in
                else if ((kvp.Value & MemberTypeInfo.BuiltIn) == MemberTypeInfo.BuiltIn)
                {
                    string name = parser.RemoveFieldsUnderscore(kvp.Key.Name);
                    IDictionary<string, object> dapperDict;
                    if (!cleanResult)
                        dapperDict = parser.GetTypeMembersWithoutPrePostFixes(dapperResult.First(), NamesList) as IDictionary<string, object>;
                    else
                        dapperDict = dapperResult.First() as IDictionary<string, object>;

                    if (!dapperDict.ContainsKey(name))
                        throw new CustomException_DapperMapper(
                            $@"DapperMapper.Map: There's no member in dynamic dapper result with name {kvp.Key.Name}. Cannot Map object.");

                    try
                    {
                        if (kvp.Key.MemberType == MemberTypes.Property) ((PropertyInfo)kvp.Key).SetValue(mapped, dapperDict[name]);
                        else ((FieldInfo)kvp.Key).SetValue(mapped, dapperDict[name]);
                    }
                    catch (Exception err)
                    {
                        throw new CustomException_DapperMapper(
                            $@"DapperMapper.Map: Couldn't map BuiltIn-type member {kvp.Key.Name} with value contained by dynamic object.
Incorrect type of value?: {kvp.Value.ToString()}",
                            err);
                    }
                }
                //if nested
                else if ((kvp.Value & MemberTypeInfo.Nested) == MemberTypeInfo.Nested)
                {
                    Type mType = GetMemberType(kvp.Key);

                    if ((kvp.Value & MemberTypeInfo.Interface) == MemberTypeInfo.Interface)
                        mType = ResolveInterface(mType, dapperResult);

                    //access generic Map method through nongeneric interface method
                    iDapperMapper nestedMapper = MappersStore.GetMapper(mType);

                    if (nestedMapper == null)
                        throw new CustomException_DapperMapper(
                            $@"DapperMapper.Map: No Mapper found at store for property {kvp.Key.Name} of type {mType.ToString()}.
If you want to map a nested property you have to create a mapper for that property type.");

                    if (kvp.Key.MemberType == MemberTypes.Property)
                        ((PropertyInfo)kvp.Key).SetValue(mapped, nestedMapper.NoGenericMap(dapperResult, cleanResult));
                    else ((FieldInfo)kvp.Key).SetValue(mapped, nestedMapper.NoGenericMap(dapperResult, cleanResult));
                }
            }

            return mapped;
        }
        /// <summary>
        /// Generic map to IEnumerables. Result HAVE to be ordered by the splitOn column.
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="dapperResult"></param>
        /// <param name="splitOn"></param>
        /// <param name="cleanResult"></param>
        /// <returns></returns>
        public R Map<R>(IEnumerable<dynamic> dapperResult, string splitOn = "Id", bool cleanResult = false)
            where R : IEnumerable<T>
        {
            R result;
            Type r = typeof(R);
            List<dynamic> singleObjectDynamic = new List<dynamic>();
            object splitObject = (dapperResult.First() as IDictionary<string, object>)[splitOn];

            if (typeof(IList).IsAssignableFrom(r))
            {
                result = (R)Activator.CreateInstance(typeof(List<>).MakeGenericType(this.TType));

                foreach (dynamic dyn in dapperResult)
                {
                    IDictionary<string, object> dict = dyn as IDictionary<string, object>;
                    if (!dict.ContainsKey(splitOn))
                        throw new CustomException_DapperMapper(
                            $@"DapperMapper.Map(IEnumerable): Dapper result doesn't have a member with name equals to splitOn parameter.
SplitOn = {splitOn}");

                    if (!object.Equals(splitObject, dict[splitOn]) || dapperResult.Last() == dyn)
                    {
                        ((IList)result).Add(Map(singleObjectDynamic));
                        singleObjectDynamic.Clear();
                        splitObject = dict[splitOn];
                    }
                    else
                        singleObjectDynamic.Add(dyn);
                }
            }
            else
            {
                //http://stackoverflow.com/questions/18251587/assign-any-ienumerable-to-object-property
                var addMethod = r.GetMethod("Add", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                // Property doesn't support Adding
                if (addMethod == null)
                    throw new CustomException_DapperMapper(
                        $@"DapperMapper.Map(IEnumerable): Method Add doesn't exist in enumerable type to map.
enumType: {r.Name}.");

                if (r.IsGenericTypeDefinition) result = (R)Activator.CreateInstance(r.MakeGenericType(this.TType));
                else result = (R)Activator.CreateInstance(r);

                foreach (dynamic dyn in dapperResult)
                {
                    IDictionary<string, object> dict = dyn as IDictionary<string, object>;
                    if (!dict.ContainsKey(splitOn))
                        throw new CustomException_DapperMapper(
                            $@"DapperMapper.Map(IEnumerable): Dapper result doesn't have a member with name equals to splitOn parameter.
SplitOn = {splitOn}");

                    if (!object.Equals(splitObject, dict[splitOn]) || dapperResult.Last() == dyn)
                    {
                        addMethod.Invoke(result, new object[] { NoGenericMap(dyn) });
                        singleObjectDynamic.Clear();
                        splitObject = dict[splitOn];
                    }
                    else
                        singleObjectDynamic.Add(dyn);
                }
            }

            return result;
        }
        /// <summary>
        /// Non-generic Map.
        /// </summary>
        /// <param name="dapperResult"></param>
        /// <returns></returns>
        public object NoGenericMap(dynamic dapperResult, bool cleanResult = false)
        {
            IEnumerable<dynamic> ienum = new List<dynamic>() { dapperResult } as IEnumerable<dynamic>;
            return this.Map(ienum, cleanResult);
        }
        /// <summary>
        /// Non-generic Map.
        /// </summary>
        /// <param name="dapperResult"></param>
        /// <param name="cleanResult"></param>
        /// <returns></returns>
        public object NoGenericMap(IEnumerable<dynamic> dapperResult, bool cleanResult = false)
        {
            return this.Map(dapperResult, cleanResult);
        }
        #endregion
    }
}


