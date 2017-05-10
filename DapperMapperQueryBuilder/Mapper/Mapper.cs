using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
//using System.Globalization;
//using System.Runtime.InteropServices;
using System.Collections;
using System.Dynamic;
using MQBStatic;
using Exceptions;

namespace Mapper
{
    [Flags]
    public enum MemberTypeInfo { BuiltIn = 1, Nested = 2, Creator = 4, IEnumerable = 8, Dictionary = 16, Interface = 32 }
    
    public interface iDapperMapper
    {
        //PropertyInfo[] pInfos { get; }
        //FieldInfo[] fInfos { get; }
        Type TType { get; }
        IEnumerable<string> NamesList { get; }
        Tuple<string[], bool> Prefixes { get; }
        Tuple<string[], bool> Postfixes { get; }

        IEnumerable<dynamic> GetDistinctDapperResult(IEnumerable<dynamic> origDapperResult, bool cleanResult);
        object NoGenericMap(dynamic dapperResult, bool cleanResult = false);
        object NoGenericMap(IEnumerable<dynamic> dapperResult, bool cleanResult = false);
        bool CheckIfDynamicHasAllTypeMembersByName(dynamic dyn);
    }

    public class MapperStore : DMStatic_Store
    {
        /// <summary>
        /// Store t as a type that can be, and have been configurated for, mapped by DapperMapper
        /// </summary>
        /// <param name="t"></param>
        public void StoreType(Type t)
        {
            if(!_TypesToMap.Contains(t))
                _TypesToMap.Add(t);
        }
        /// <summary>
        /// Store a mapper
        /// </summary>
        /// <param name="t"></param>
        /// <param name="mapper"></param>
        public void StoreMapper(Type t, iDapperMapper mapper)
        {
            if(!_Mappers.ContainsKey(t)) _Mappers.Add(t, mapper);
        }
        /// <summary>
        /// Get mapper of type t. If type t haven't been stored by StoreType, returns null. If type t have been stored and there are no mapper
        /// created yet, it creates a new one, store it, and return it.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public iDapperMapper GetMapper(Type t)
        {
            if(_Mappers.ContainsKey(t))
                return _Mappers[t];

            if (!_TypesToMap.Contains(t))
                return null;

            iDapperMapper mapper = (iDapperMapper)Activator.CreateInstance(typeof(DapperMapper<>).MakeGenericType(t), this);
            StoreMapper(t, mapper);
            return mapper;
            //return null;
        }
        /// <summary>
        /// Returns true if a mapper exists or can be created, and set it as iDapperMapper.
        /// If type t haven't been stored by StoreType, returns false. If type t have been stored and there are no mapper
        /// created yet, it creates a new one, store it, set it as iDapperMapper and returns true.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="mapper"></param>
        /// <returns></returns>
        public bool GetMapper(Type t, out iDapperMapper mapper)
        {
            if (_Mappers.ContainsKey(t))
            {
                mapper = _Mappers[t];
                return true;
            }

            if (!_TypesToMap.Contains(t))
            {
                mapper = null;
                return false;
            }

            mapper = (iDapperMapper)Activator.CreateInstance(typeof(DapperMapper<>).MakeGenericType(t), this);
            StoreMapper(t, mapper);
            return false;
        }
        /// <summary>
        /// Remove mapper previously stored.
        /// </summary>
        /// <param name="t"></param>
        public void RemoveMapper(Type t)
        {
            if (_Mappers.ContainsKey(t))
                _Mappers.Remove(t);
        }
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
        //public PropertyInfo[] pInfos { get; private set; }
        //public FieldInfo[] fInfos { get; private set; }
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
        public bool CheckIfDynamicHasAllTypeMembersByName(IDictionary<string, object> membersDict)
        {
            IEnumerable<string> dynList = membersDict.Select(kvp => kvp.Key);
            PrePostFixesParser parser = new PrePostFixesParser(this);

            return dynList.SequenceEqual(parser.GetCleanNamesList(this.NamesList));
        }
        /// <summary>
        /// Generic Map
        /// </summary>
        /// <param name="dapperResult"></param>
        /// <returns></returns>
        public T Map(IEnumerable<dynamic> dapperResult, bool cleanResult = false)
        {
            var parser = new PrePostFixesParser(this);
            T mapped = NewObject(dapperResult.First());

            //TODO: divide el siguiente foreach en dos con dos nuevos diccionarios estáticos, uno para pInfos y otro para fInfos, 
            //aunque se repita código: hacer métodos para cada parte del código del tipo:
            //private T PreMapCreator(KeyValuePair<PropertyInfo, MemberTypeInfo> kvp, IEnumerable<dynamic> dapperResult, bool cleanResult = false)
            //private T PreMapIEnumerable(KeyValuePair<PropertyInfo, MemberTypeInfo> kvp, IEnumerable<dynamic> dapperResult, bool cleanResult = false)
            //...
            
            //Loop through all members
            foreach (KeyValuePair<MemberInfo, MemberTypeInfo> kvp in mtInfos)
            {
                //Member have a creator
                if ((kvp.Value & MemberTypeInfo.Creator) == MemberTypeInfo.Creator)
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
        /// Non-generic Map
        /// </summary>
        /// <param name="dapperResult"></param>
        /// <returns></returns>
        public object NoGenericMap(dynamic dapperResult, bool cleanResult = false)
        {
            IEnumerable<dynamic> ienum = new List<dynamic>() { dapperResult } as IEnumerable<dynamic>;
            return this.Map(ienum, cleanResult);
        }
        public object NoGenericMap(IEnumerable<dynamic> dapperResult, bool cleanResult = false)
        {
            return this.Map(dapperResult, cleanResult);
        }
        #endregion
    }

    #region oldMapper
    //if (_Constructors.ContainsKey(this.TType))
    //{
    //    /*ConstructorDelegate cd = (ConstructorDelegate)_Constructors[this.TType];
    //    var cParams = cd.GetMethodInfo().GetParameters();
    //    foreach(ParameterInfo param in cParams)
    //        ok = ok && membersDict.ContainsKey(param.Name);

    //    if (!ok) return false;*/
    //    ConstructorDelegate cd = (ConstructorDelegate)_Constructors[this.TType];
    //    cParams = cd.GetMethodInfo().GetParameters();
    //    namesList = cParams.Select(param => param.Name);
    //}
    //if (_MembersCreators.ContainsKey(this.TType))
    //{
    //    foreach (KeyValuePair<string, Delegate> kvp in _MembersCreators[this.TType])
    //    {
    //        /*MemberDelegate md = (MemberDelegate)kvp.Value;
    //        var cParams = md.GetMethodInfo().GetParameters();
    //        foreach (ParameterInfo param in cParams)
    //            ok = ok && membersDict.ContainsKey(param.Name);

    //        if (!ok) return false;*/
    //        MemberDelegate md = (MemberDelegate)kvp.Value;
    //        cParams = md.GetMethodInfo().GetParameters();
    //        namesList = namesList.Union(cParams.Select(param => param.Name));
    //    }
    //}
    //if (_NestedProperties.ContainsKey(this.TType))
    //{
    //    /*foreach(string str in _NestedProperties[this.TType])
    //    {
    //        iDapperMapper mapper = this.MappersStore.GetMapper(this.pInfos.Where(pInfo => pInfo.Name == str).GetType());
    //        ok = ok && mapper.CheckIfDynamicHasAllTypeMembersByName(dyn);

    //        if (!ok) return false;
    //    }*/
    //    namesList = namesList.Union(_NestedProperties[this.TType]);
    //}
    /*private bool TryCreateMemberWithDelegate(ref T instance, PropertyInfo pInfo)
    {
        if (_MembersCreators.ContainsKey(this.TType) && _MembersCreators[this.TType].ContainsKey(pInfo.Name))
        {
            MemberDelegate mDel = (MemberDelegate)_MembersCreators[this.TType][pInfo.Name];
            pInfo.SetValue(instance, mDel(GetMemberParams(pInfo.Name)));
            return true;
        }
        return false;
    }
    private bool TryCreateMemberWithDelegate(ref T instance, FieldInfo pInfo)
    {
        if (_MembersCreators.ContainsKey(this.TType) && _MembersCreators[this.TType].ContainsKey(pInfo.Name))
        {
            MemberDelegate mDel = (MemberDelegate)_MembersCreators[this.TType][pInfo.Name];
            pInfo.SetValue(instance, mDel(GetMemberParams(pInfo.Name)));
            return true;
        }
        return false;
    }
    private bool TryMapNestedMember(ref T instance, ref dynamic dapperResult, PropertyInfo pInfo)
    {
        if(_NestedProperties[this.TType].Contains(pInfo.Name))
        {
            Type pType = pInfo.GetType();

            //access generic Map method through nongeneric interface method
            pInfo.SetValue(instance, _Mappers[pType].NoGenericMap(dapperResult));
            return true;
        }
        return false;
    }
    private bool TryMapNestedMember(ref T instance, ref dynamic dapperResult, FieldInfo pInfo)
    {
        if (_NestedProperties[this.TType].Contains(pInfo.Name))
        {
            Type pType = pInfo.GetType();

            //access generic Map method through nongeneric interface method
            pInfo.SetValue(instance, _Mappers[pType].NoGenericMap(dapperResult));
            return true;
        }
        return false;
    }*/
    /*private IEnumerable<dynamic> GetDictionarySubDynamics(ref IEnumerable<dynamic> supDynamic, MemberInfo mInfo, Type memberType)
{
Type[] genericTypes = memberType.GenericTypeArguments;
Type keysType = instanceDict.Keys.ElementAt(0).GetType();
Type valuesType = instanceDict.ElementAt(0).Value.GetType();
foreach (dynamic dyn in supDynamic)
{
    if()
}
}*/
    /*
    //If the member is IEnumerable then first do ienumerable
    //MemberTypeInfo firstEnumerableFlag =
    //(kvp.Value & MemberTypeInfo.IEnumerable) != MemberTypeInfo.IEnumerable ? MemberTypeInfo.IEnumerable : kvp.Value;

                    /*iDapperMapper nestedMapper;
                    //One member can have various MemberTypeInfo flags, f.i. it can be an enumerable and nested type,
                    //so loop through correspondent MemberTypeInfo flags
                    bool finished = false;
                    while (!finished)
                    {
                        switch (firstEnumerableFlag)
                        { 
                            case MemberTypeInfo.IEnumerable:
                                //Type t = (kvp.Key as PropertyInfo) != null ? ((PropertyInfo)kvp.Key).PropertyType : ((FieldInfo)kvp.Key).FieldType;
                                Type t;
                                //Type of property or field
                                if (kvp.Key.MemberType == MemberTypes.Property) t = ((PropertyInfo)kvp.Key).PropertyType;
                                else t = ((FieldInfo)kvp.Key).FieldType;

                                //If member is a dictionary
                                if (typeof(IDictionary).IsAssignableFrom(t))
                                {
                                    //Create a dummy dictionary with the dapper's dynamic result which should be equal to the final one
                                    IDictionary<object, object> dummyDict = GetDummyDictionary(dResult);

                                    //If all types are correct, set member
                                    if (AllDictTypesAreCorrect(t, dummyDict))
                                    {
                                        object intermediate = (IDictionary)Activator.CreateInstance(t); //Original member type object
                                        foreach (KeyValuePair<object, object> dKvp in dummyDict)
                                            ((IDictionary)intermediate).Add(dKvp.Key, dKvp.Value);

                                        if (kvp.Key.MemberType == MemberTypes.Property)
                                        {
                                            PropertyInfo pInfo = (PropertyInfo)kvp.Key;
                                            pInfo.SetValue(mapped, intermediate);
                                        }
                                        else
                                        {
                                            FieldInfo fInfo = (FieldInfo)kvp.Key;
                                            fInfo.SetValue(mapped, intermediate);
                                        }
                                    }
                                    else throw new CustomException_DapperMapper(
                                        $@"DapperMapper.Map: Generic types of Dapper result don't agree with member {kvp.Key.Name} to be mapped");
                                }                            
                                //Rest of enumerables
                                else
                                {
                                    if (kvp.Key.MemberType == MemberTypes.Property)
                                    {
                                        IEnumerable<object> intermediate = DapperResultDict
                                            .Where(x => x.Key == ((PropertyInfo)kvp.Key).Name) //TODO: OJO esto podria estar mal, en vez del nombre 
                                            //del miembro actual, en el dynamic podrian estar solo los nombres de los miembros nested,
                                            //esto se podria repetir a cada paso del bucle
                                            .Select(x =>
                                            {
                                                //if the generic type of the IEnumerable is built-in return value
                                                nestedMapper = this.MappersStore.GetMapper(t.GetGenericArguments()[0]);
                                                if (nestedMapper == null)
                                                    return x.Value;
                                                //else is nested, map it
                                                return nestedMapper.Map(dapperResult);
                                            });

                                        ((PropertyInfo)kvp.Key).SetValue(mapped, intermediate);
                                    }
                                    else
                                    {
                                        IEnumerable<object> intermediate = DapperResultDict
                                            .Where(x => x.Key == ((FieldInfo)kvp.Key).Name)
                                            .Select(x =>
                                            {
                                                //if the generic type of the IEnumerable is built-in return value
                                                nestedMapper = this.MappersStore.GetMapper(t.GetGenericArguments()[0]);
                                                if (nestedMapper == null)
                                                    return x.Value;
                                                //else is nested, map it
                                                return nestedMapper.Map(dapperResult);
                                            });

                                        ((FieldInfo)kvp.Key).SetValue(mapped, intermediate);
                                    }
                                }

                                /*int[] prueba = new int[] { 1, 2 };

                                IEnumerable<int> p2 = (ICollection<int>)prueba;
                                List<dynamic> dynamics = new List<dynamic>();
                                Type ienumType = kvp.Key.GetType();
                                Type[] genericTypes = ienumType.GetGenericTypeDefinition().GetGenericArguments();
                                ienumType.MakeGenericType(genericTypes);
                                var ienumMember = Activator.CreateInstance(ienumType);
                                var cast = dynamics.GetType().GetMethod("Cast").MakeGenericMethod(ienumType);

                                if (genericTypes.Count() < 2)
                                {
                                    if (typeof(ICollection<>).IsAssignableFrom(ienumType))
                                    {
                                        foreach (Type t in genericTypes)
                                        {
                                            foreach (dynamic dyn in dapperResult.Skip(1))
                                            {

                                            }
                                        }
                                    }
                                }*/
    //Remove the IEnumerable flag and continue with the next and final flag if it exists
    /*firstEnumerableFlag = kvp.Value ^ MemberTypeInfo.IEnumerable;
    break;
case MemberTypeInfo.BuiltIn:
    if (!DapperResultDict.ContainsKey(kvp.Key.Name))
        throw new CustomException_DapperMapper(
            $@"DapperMapper.Map: There's no member in dynamic dapper result with name {kvp.Key.Name}. Cannot Map object.");

    try
    {
        if (kvp.Key.MemberType == MemberTypes.Property) ((PropertyInfo)kvp.Key).SetValue(mapped, DapperResultDict[kvp.Key.Name]);
        else ((FieldInfo)kvp.Key).SetValue(mapped, DapperResultDict[kvp.Key.Name]);
    }
    catch(Exception err)
    {
        throw new CustomException_DapperMapper(
            $@"DapperMapper.Map: Couldn't map BuiltIn-type member {kvp.Key.Name} with value contained by dynamic object.
Incorrect type of value: {kvp.Value.ToString()} ?");
    }

    finished = true;
    break;
case MemberTypeInfo.Creator:

    finished = true;
    break;
case MemberTypeInfo.Nested:
    Type mType = kvp.Key.GetType();

    //access generic Map method through nongeneric interface method
    if (kvp.Key.MemberType == MemberTypes.Property) ((PropertyInfo)kvp.Key).SetValue(mapped, _Mappers[pType].NoGenericMap(dapperResult));
    else ((FieldInfo)kvp.Key).SetValue(mapped, _Mappers[pType].NoGenericMap(dapperResult));*/
    /*nestedMapper = MappersStore.GetMapper(mType);

    if (nestedMapper == null)
        throw new CustomException_DapperMapper(
            $@"DapperMapper.Map: No Mapper found at store for property {kvp.Key.Name} of type {mType.ToString()}.
If you want to map a nested property you have to create a mapper for that property type.");
    if (kvp.Key.MemberType == MemberTypes.Property)
        ((PropertyInfo)kvp.Key).SetValue(mapped, nestedMapper.Map(dapperResult));

    finished = true;
    break;
default:
    finished = true;
    break;
}
}*/
    /*foreach(PropertyInfo pInfo in pInfos)
    {
        //If a MemberCreator exists for this member, set member value with the creator, otherwise check if member is nested
        //and use a new mapper of corresponding type for set the member value
        if (!TryCreateMemberWithDelegate(ref mapped, pInfo) && !TryMapNestedMember(ref mapped, ref dResult, pInfo))
        {
            if()
            {
                dynamic dynList = dapperResult.Skip(enumerableMembersOrder - 1).Take(1);
            }

            if (!DapperResultDict.ContainsKey(pInfo.Name))
                throw new CustomException_DapperMapper(
                    $@"DapperMapper.Map: There's no member in dynamic object with name {pInfo.Name}. Cannot Map object.");

            pInfo.SetValue(mapped, DapperResultDict[pInfo.Name]);
        }
    }
    foreach (FieldInfo fInfo in fInfos)
    {
        //If a MemberCreator exists for this member, set member value with the creator, otherwise check if member is nested
        //and use a new mapper of corresponding type for set the member value
        if (!TryCreateMemberWithDelegate(ref mapped, fInfo) && !TryMapNestedMember(ref mapped, ref dResult, fInfo))
        {
            if (!DapperResultDict.ContainsKey(fInfo.Name))
                throw new CustomException_DapperMapper(
                    $@"DapperMapper.Map: There's no member in dynamic object with name {fInfo.Name}. Cannot Map object.");

            fInfo.SetValue(mapped, DapperResultDict[fInfo.Name]);
        }
    }*/
    #endregion
}


