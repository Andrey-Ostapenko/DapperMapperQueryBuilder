using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mapper
{
    public class PrePostFixesParser
    {
        public PrePostFixesParser(iDapperMapper mapper)
        {
            Tuple<string[], bool> prefixes = mapper != null ? mapper.Prefixes : null;
            Tuple<string[], bool> postfixes = mapper != null ? mapper.Postfixes : null;

            if (prefixes != null)
            {
                this.Prefixes = prefixes.Item1;
                this.PrefixesExclusive = prefixes.Item2;
                this.PrefixesCount = prefixes.Item1.Select(x => x.Count()).Distinct();
            }
            else
            {
                this.Prefixes = null;
                this.PrefixesExclusive = false;
                this.PrefixesCount = new int[] { 0 };
            }

            if (postfixes != null)
            {
                this.Postfixes = postfixes.Item1;
                this.PostfixesExclusive = postfixes.Item2;
                this.PostfixesCount = postfixes.Item1.Select(x => x.Count()).Distinct();
            }
        }

        #region properties
        public string[] Prefixes { get; private set; }
        public string[] Postfixes { get; private set; }
        public bool PrefixesExclusive { get; private set; }
        public bool PostfixesExclusive { get; private set; }
        public IEnumerable<int> PrefixesCount { get; private set; }
        public IEnumerable<int> PostfixesCount { get; private set; }
        #endregion
        
        #region helpers
        private bool RemovePrefixIfStringContains(ref string str)
        {
            if (Prefixes == null)
                return false;

            bool contain = false;
            foreach (int count in PrefixesCount)
            {
                //contains some of the configurated prefixes
                contain = Prefixes.Contains(str.Substring(0, count));
                if (contain)
                {
                    str = str.Remove(0, count);
                    return contain;
                }
            }

            return contain;
        }
        private bool RemovePostfixIfStringContains(ref string str)
        {
            if (Postfixes == null)//(PostfixesCount.Count() == 1 && PostfixesCount.First() == 0)
                return false;

            bool contain = false;
            foreach (int count in PostfixesCount)
            {
                contain = Postfixes.Contains(str.Substring(str.Length - count, count));
                if (contain)
                {
                    str = str.Remove(str.Length - count, count);
                    return contain;
                }
            }
            return contain;
        }
        private bool KeysContainsSomePrePostfix(IDictionary<string, object> dict)
        {
            if (Prefixes != null && Postfixes != null)
            {
                return dict.Any(kvp =>
                {
                    foreach (int count in PrefixesCount)
                    {
                        //contains some of the configurated prefixes or postfixes
                        if (Prefixes.Contains(kvp.Key.Substring(0, count))
                            || Postfixes.Contains(kvp.Key.Substring(kvp.Key.Length - count, count)))
                            return true;
                        else return false;
                    }
                    return false;
                });
            }
            else if (Prefixes != null)
            {
                return dict.Any(kvp =>
                {
                    foreach (int count in PrefixesCount)
                    {
                        //contains some of the configurated prefixes
                        if (Prefixes.Contains(kvp.Key.Substring(0, count)))
                            return true;
                        else return false;
                    }
                    return false;
                });
            }
            else if (Postfixes != null)
            {
                return dict.Any(kvp =>
                {
                    foreach (int count in PrefixesCount)
                    {
                        //contains some of the configurated postfixes
                        if (Postfixes.Contains(kvp.Key.Substring(kvp.Key.Length - count, count)))
                            return true;
                        else return false;
                    }
                    return false;
                });
            }
            return false;
            //return dict.Any(kvp => (Prefixes.Contains(kvp.Key.Substring(0,)
        }
        #endregion

        #region public methods
        public string RemoveFieldsUnderscore(string str)
        {
            if (str.StartsWith("_")) return str.Remove(0, 1);
            return str;
        }
        public IEnumerable<string> GetCleanNamesList(IEnumerable<string> namesList)
        {
            return namesList.Select(str => RemoveFieldsUnderscore(str));
        }
        public IEnumerable<IDictionary<string, object>> RemovePrePostFixesFromDictionary(IEnumerable<dynamic> dyn)
        {
            List<Dictionary<string, object>> ienum = new List<Dictionary<string, object>>();
            foreach (dynamic d in dyn)
            {
                Dictionary<string, object> dict = new Dictionary<string, object>();
                foreach (KeyValuePair<string, object> kvp in (d as IDictionary<string, object>))
                {
                    string keyWithoutPrePostfixes = kvp.Key;
                    if (!RemovePrefixIfStringContains(ref keyWithoutPrePostfixes) && !RemovePostfixIfStringContains(ref keyWithoutPrePostfixes))
                        dict.Add(kvp.Key, kvp.Value);
                    else
                        dict.Add(keyWithoutPrePostfixes, kvp.Value);//return new KeyValuePair<string, object>(keyWithoutPrePostfixes, kvp.Value);
                }
                ienum.Add(dict);
            }

            return ienum;
        }
        public dynamic GetTypeMembersWithoutPrePostFixes(dynamic dyn, IEnumerable<string> namesList)
        {
            var typeMembers = new ExpandoObject() as IDictionary<string, object>;
            IDictionary<string, object> dict = dyn as IDictionary<string, object>;
            IEnumerable<string> names = namesList.Select(str => RemoveFieldsUnderscore(str));

            int i = 0;
            if (!PrefixesExclusive && !PostfixesExclusive)
            {
                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    string keyWithoutPrePostfixes = kvp.Key;
                    bool namesContainsKey = names.Contains(kvp.Key);

                    if (!RemovePrefixIfStringContains(ref keyWithoutPrePostfixes)
                        && !RemovePostfixIfStringContains(ref keyWithoutPrePostfixes)
                        && namesContainsKey)
                    {
                        typeMembers.Add(kvp.Key, kvp.Value);
                        i++;
                    }
                    else if (names.Contains(keyWithoutPrePostfixes))
                    {
                        typeMembers.Add(keyWithoutPrePostfixes, kvp.Value);
                        i++;
                    }
                }
            }
            else if (PrefixesExclusive && PostfixesExclusive)
            {
                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    string keyWithoutPrePostfixes = kvp.Key;
                    if (RemovePrefixIfStringContains(ref keyWithoutPrePostfixes)
                        && RemovePostfixIfStringContains(ref keyWithoutPrePostfixes)
                        && names.Contains(keyWithoutPrePostfixes))
                        typeMembers.Add(keyWithoutPrePostfixes, kvp.Value);
                }
            }
            else if (PrefixesExclusive)
            {
                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    string keyWithoutPrePostfixes = kvp.Key;
                    if (RemovePrefixIfStringContains(ref keyWithoutPrePostfixes))
                    {
                        RemovePostfixIfStringContains(ref keyWithoutPrePostfixes);

                        if (names.Contains(keyWithoutPrePostfixes))
                            typeMembers.Add(keyWithoutPrePostfixes, kvp.Value);
                    }
                }
            }
            else
            {
                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    string keyWithoutPrePostfixes = kvp.Key;
                    if (RemovePostfixIfStringContains(ref keyWithoutPrePostfixes))
                    {
                        RemovePrefixIfStringContains(ref keyWithoutPrePostfixes);

                        if (names.Contains(keyWithoutPrePostfixes))
                            typeMembers.Add(keyWithoutPrePostfixes, kvp.Value);
                    }
                }
            }

            return typeMembers;
        }
        #endregion
    }
}
