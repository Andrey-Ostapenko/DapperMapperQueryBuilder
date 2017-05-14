using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Dynamic;
using MQBStatic;
using Exceptions;

namespace Mapper
{
    public class DictionaryMapper : DMStatic_Dictionary
    {
        public DictionaryMapper(
            IEnumerable<dynamic> dResult, 
            string memberName, 
            bool nested, 
            bool isAnInterface,
            bool cleanResult, 
            Type memberType, 
            iDapperMapper masterMapper)
        {
            Type masterType = masterMapper.TType;
            if(!cleanResult && !nested)
            {
                IEnumerable<dynamic> clean;
                var parser = new PrePostFixesParser(masterMapper);
                clean = dResult.Select(dyn => parser.GetTypeMembersWithoutPrePostFixes(dyn, masterMapper.NamesList));

                GetDummyDictionary(dResult, memberName, false, isAnInterface, /*allowDuplicates, */true, memberType, masterType);
            }
            else GetDummyDictionary(dResult, memberName, nested, isAnInterface, /*allowDuplicates, */cleanResult, memberType, masterType);
        }

        #region properties
        public IDictionary DummyDictionary { get; private set; }
        #endregion

        #region result parsers
        protected dynamic GetCleanResult(IEnumerable<dynamic> result, iDapperMapper mapper, bool clean)
        {
            PrePostFixesParser parser = new PrePostFixesParser(mapper);

            if (!clean) return result.Select(dyn => parser.GetTypeMembersWithoutPrePostFixes(dyn, mapper.NamesList));
            else return result.Select(dyn => parser.RemovePrePostFixesFromDictionary(dyn));
        }
        protected dynamic GetCleanResult(dynamic dyn, iDapperMapper keysMapper, iDapperMapper valuesMapper, bool clean)
        {
            var KeysParser = new PrePostFixesParser(keysMapper);
            var ValuesParser = new PrePostFixesParser(valuesMapper);
            var typeMembers = new ExpandoObject() as IDictionary<string, object>;

            if (!clean)
            {
                typeMembers = KeysParser.GetTypeMembersWithoutPrePostFixes(dyn, keysMapper.NamesList);
                typeMembers = typeMembers
                    .Concat((IDictionary<string, object>)ValuesParser.GetTypeMembersWithoutPrePostFixes(dyn, valuesMapper.NamesList))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            else
            {
                typeMembers = KeysParser.RemovePrePostFixesFromDictionary(dyn);
                typeMembers = typeMembers
                    .Concat((IDictionary<string, object>)ValuesParser.RemovePrePostFixesFromDictionary(dyn))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            return typeMembers;
        }
        protected dynamic GetCleanResult(dynamic dyn, iDapperMapper keysMapper, string valuesName, bool clean)
        {
            var KeysParser = new PrePostFixesParser(keysMapper);
            var typeMembers = new ExpandoObject() as IDictionary<string, object>;

            if (!clean)
            {
                typeMembers = KeysParser.GetTypeMembersWithoutPrePostFixes(dyn, keysMapper.NamesList);
                typeMembers.Add(valuesName, ((IDictionary<string, object>)dyn)[valuesName]);
            }
            else
            {
                typeMembers = KeysParser.RemovePrePostFixesFromDictionary(dyn);
                typeMembers.Add(valuesName, ((IDictionary<string, object>)dyn)[valuesName]);
            }

            return typeMembers;
        }
        protected dynamic GetCleanResult(dynamic dyn, string keysName, iDapperMapper valuesMapper, bool clean)
        {
            var ValuesParser = new PrePostFixesParser(valuesMapper);
            var typeMembers = new ExpandoObject() as IDictionary<string, object>;

            if (!clean)
            {
                typeMembers = ValuesParser.GetTypeMembersWithoutPrePostFixes(dyn, valuesMapper.NamesList);
                typeMembers.Add(keysName, ((IDictionary<string, object>)dyn)[keysName]);
            }
            else
            {
                typeMembers = ValuesParser.RemovePrePostFixesFromDictionary(dyn);
                typeMembers.Add(keysName, ((IDictionary<string, object>)dyn)[keysName]);
            }

            return typeMembers;
        }
        #endregion

        #region helpers
        private object GetDefault(Type t)
        {
            return t.IsValueType ? Activator.CreateInstance(t) : null;
        }
        private void GetDummyDictionary(
            IEnumerable<dynamic> dResult, 
            string memberName, 
            bool nested, 
            bool isAnInterface,
            bool cleanResult,
            Type dictType, 
            Type masterType)
        {
            string[] keyValueNames = _Dictionaries[masterType][memberName];
            Type keysType = dictType.GenericTypeArguments[0]; //keys types of dummy dict
            Type valuesType = dictType.GenericTypeArguments[1]; //values types of dummy dict
            this.DummyDictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(dictType.GenericTypeArguments));

            if (!nested)
            {
                bool noKeys = true;
                foreach (dynamic dyn in dResult)
                {
                    IDictionary<string, object> dict = dyn as IDictionary<string, object>;
                    if (!dict.ContainsKey(keyValueNames[0])) continue;
                    if (this.DummyDictionary.Contains(dict[keyValueNames[0]])) continue;

                    noKeys = false;
                    if (!dict.ContainsKey(keyValueNames[1]))
                        DummyDictionary.Add(dict[keyValueNames[0]], GetDefault(valuesType));
                    else
                        DummyDictionary.Add(dict[keyValueNames[0]], dict[keyValueNames[1]]);
                }
                //if there aren't any key correct, exception
                if (noKeys)//preDummy == null)//(!preDummy.Any(dyn => dyn.ContainsKey(keyValueNames[0])))
                    throw new CustomException_DapperMapper(
                        $@"DictionaryMapper.GetDummyDictionary: Dynamic dapper result doesn't have members with the name 
required to create dictionary {memberName} keys in {masterType.Name}. Key-value names configurated: ({keyValueNames[0]}, {keyValueNames[1]})");
            }
            else
            {
                MapperStore store = new MapperStore();

                if(isAnInterface)
                {
                    bool keysAreInterface = 
                        keysType.IsInterface && 
                        !typeof(IDictionary).IsAssignableFrom(keysType) &&
                        !(typeof(IEnumerable).IsAssignableFrom(keysType) && !typeof(string).IsAssignableFrom(keysType));
                    bool valuesAreInterface = 
                        valuesType.IsInterface &&
                        !typeof(IDictionary).IsAssignableFrom(valuesType) &&
                        !(typeof(IEnumerable).IsAssignableFrom(valuesType) && !typeof(string).IsAssignableFrom(valuesType));

                    if (keysAreInterface)
                        keysType = ResolveInterface(keysType, dResult);
                    if (valuesAreInterface)
                        valuesType = ResolveInterface(valuesType, dResult);
                }
                iDapperMapper keysNestedMapper = store.GetMapper(keysType);
                iDapperMapper valuesNestedMapper = store.GetMapper(valuesType);

                //if there aren't any key correct, exception
                if (keysNestedMapper != null && !keysNestedMapper.CheckIfDynamicHasAllTypeMembersByName(dResult.First()))
                    throw new CustomException_DapperMapper(
                        $@"DictionaryMapper.GetDummyDictionary: Dynamic dapper result doesn't have members with the name 
required to create dictionary {memberName} in {masterType.Name}. Key-value names configurated: ({keyValueNames[0]}, {keyValueNames[1]})");

                //if both keys and values are nested types
                if (keysNestedMapper != null && valuesNestedMapper != null)
                {
                    foreach (dynamic dyn in dResult)
                    {
                        dynamic cleanDyn = GetCleanResult(dyn, keysNestedMapper, valuesNestedMapper, cleanResult);
                        object key = keysNestedMapper.NoGenericMap(cleanDyn, true);

                        if (this.DummyDictionary.Contains(key)) continue;

                        this.DummyDictionary.Add(
                            key,
                            valuesNestedMapper.NoGenericMap(cleanDyn, true));
                    }
                }
                //if only values are nested
                else if (keysNestedMapper == null)
                {
                    foreach (dynamic dyn in dResult)
                    {
                        IDictionary<string, object> dict = (IDictionary<string, object>)dyn;
                        if (!dict.ContainsKey(keyValueNames[0])) continue;
                        
                        dynamic cleanDyn = GetCleanResult(dyn, keyValueNames[0], valuesNestedMapper, cleanResult);
                        dict = (IDictionary<string, object>)cleanDyn;
                        
                        if (this.DummyDictionary.Contains(dict[keyValueNames[0]])) continue;

                        if (!dict.ContainsKey(keyValueNames[1]))
                            this.DummyDictionary.Add(dict[keyValueNames[0]], GetDefault(valuesType));
                        else
                            this.DummyDictionary.Add(dict[keyValueNames[0]], valuesNestedMapper.NoGenericMap(cleanDyn, true));
                    }
                }
                //if only keys are nested
                else
                {
                    //bool noKeys = true;
                    foreach (dynamic dyn in dResult)
                    {
                        IDictionary<string, object> dict = (IDictionary<string, object>)dyn;
                        //if (!keysNestedMapper.CheckIfDynamicHasAllTypeMembersByName(dyn)) continue;

                        //noKeys = false;
                        dynamic cleanDyn = GetCleanResult(dyn, keysNestedMapper, keyValueNames[1], cleanResult);
                        dict = (IDictionary<string, object>)cleanDyn;

                        object key = keysNestedMapper.NoGenericMap(cleanDyn, true);
                        if (this.DummyDictionary.Contains(key)) continue;

                        if (!dict.ContainsKey(keyValueNames[1]))
                            this.DummyDictionary.Add(key, GetDefault(valuesType));
                        else
                            this.DummyDictionary.Add(key, dict[keyValueNames[1]]);
                    }
                    //if there aren't any key correct, exception
//                    if (noKeys)//preDummy == null)
//                        throw new CustomException_DapperMapper(
//                            $@"DictionaryMapper.GetDummyDictionary: Dynamic dapper result doesn't have members with the name 
//required to create dictionary {memberName} in {masterType.Name}. Key-value names configurated: ({keyValueNames[0]}, {keyValueNames[1]})");
                }
            }
        }
        #endregion
    }

    public class EnumerableMapper : MQBStatic_IEnumerable
    {
        public EnumerableMapper(IEnumerable<dynamic> dResult, string memberName, bool nested, Type memberType, Type masterType)
        {
            bool allowDups = _AllowDuplicates.ContainsKey(masterType) ?
                _AllowDuplicates[masterType].Contains(memberName) :
                false;

            GetDummyEnumerable(dResult, memberName, nested, allowDups, memberType, masterType);
            this._Type = memberType;
        }

        #region fields
        private Type _Type;
        #endregion

        #region properties
        public IEnumerable DummyEnumerable { get; private set; }
        #endregion

        #region helpers
        private void GetDummyEnumerable(IEnumerable<dynamic> cleanDapperResult, string memberName, bool nested, bool allowDuplicates, Type enumType, Type TType)
        {
            if (!nested)
            {
                if (!allowDuplicates)
                    this.DummyEnumerable = cleanDapperResult
                        .Select(dyn => (IDictionary<string, object>)dyn)
                        .Select(dict => dict[memberName])
                        .Distinct();
                else
                    this.DummyEnumerable = cleanDapperResult
                        .Select(dyn => (IDictionary<string, object>)dyn)
                        .Select(dict => dict[memberName]);
            }
            else
            {
                Type genericType = enumType.GenericTypeArguments[0];
                bool genericIsInterface =
                        genericType.IsInterface &&
                        !typeof(IDictionary).IsAssignableFrom(genericType) &&
                        !(typeof(IEnumerable).IsAssignableFrom(genericType) && !typeof(string).IsAssignableFrom(genericType));
                if (genericIsInterface) genericType = ResolveInterface(genericType, cleanDapperResult);
                MapperStore store = new MapperStore();
                iDapperMapper nestedMapper = store.GetMapper(genericType);

                if (typeof(IList).IsAssignableFrom(enumType))
                {
                    IList result = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(genericType));
                    if(!allowDuplicates)
                    {
                        IEnumerable<dynamic> distinctCleanResult = nestedMapper.GetDistinctDapperResult(cleanDapperResult, true);
                        foreach (dynamic dyn in distinctCleanResult)
                            result.Add(nestedMapper.NoGenericMap(dyn, true));
                    }
                    else
                        foreach (dynamic dyn in cleanDapperResult)
                            result.Add(nestedMapper.NoGenericMap(dyn, true));
                    this.DummyEnumerable = result;
                }
                else
                {
                    //http://stackoverflow.com/questions/18251587/assign-any-ienumerable-to-object-property
                    var addMethod = enumType.GetMethod("Add", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    // Property doesn't support Adding
                    if (addMethod == null) //throw new InvalidOperationException("Method Add does not exist on class " + enumType.Name);
                        throw new CustomException_DapperMapper(
                            $@"EnumerableMapper.GetDummyEnumerable: Method Add doesn't exist in enumerable type to map.
enumType: {enumType.Name} ; member name: {memberName}.");

                    if (enumType.IsGenericTypeDefinition) this.DummyEnumerable = (IEnumerable)Activator.CreateInstance(enumType.MakeGenericType(genericType));
                    else this.DummyEnumerable = (IEnumerable)Activator.CreateInstance(enumType);

                    if (!allowDuplicates)
                    {
                        IEnumerable<dynamic> distinctCleanResult = nestedMapper.GetDistinctDapperResult(cleanDapperResult, true);
                        foreach (dynamic dyn in distinctCleanResult)
                            addMethod.Invoke(this.DummyEnumerable, new object[] { nestedMapper.NoGenericMap(dyn, true) });
                    }
                    else
                        foreach (dynamic dyn in cleanDapperResult)
                            addMethod.Invoke(this.DummyEnumerable, new object[] { nestedMapper.NoGenericMap(dyn, true) });
                }
            }
        }
        #endregion
    }

}
