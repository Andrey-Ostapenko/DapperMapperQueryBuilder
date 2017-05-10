using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Dynamic;
using System.Reflection;
using MQBStatic;
using Exceptions;

namespace QBuilder
{
    public class QueryBuilder : StringSQLBuilder
    {
        public QueryBuilder()
        {
            _Parameters = new ExpandoObject() as IDictionary<string, object>;
        }

        #region fields
        private IDictionary<string, object> _Parameters;
        #endregion

        #region properties
        public dynamic Parameters { get { return this._Parameters; } }
        #endregion

        #region helpers
        public static string MakeParameter<T, TMember>(Expression<Func<T, TMember>> expression)
        {
            //http://stackoverflow.com/questions/273941/get-property-name-and-type-using-lambda-expression
            var member = expression.Body as MemberExpression;
            if (member != null)
                return member.Member.Name.PutAhead("@");

            throw new CustomException_StringSQLBuilder($"QBuilder.MakeParameter:Expression {expression} is not a member access");
        }
        public void StoreParameter(string name, object value)
        {
            _Parameters.Add(name, value);
        }
        public void StoreParametersFrom<T>(T obj, string paramSuffix = "")
        {
            Type t = typeof(T);

            foreach (PropertyInfo pInfo in _QBPropertyInfos[t]) this._Parameters.Add(pInfo.Name + paramSuffix, pInfo.GetValue(obj));
            foreach (FieldInfo fInfo in _QBFieldInfos[t]) this._Parameters.Add(RemoveFieldsUnderscore(fInfo.Name) + paramSuffix, fInfo.GetValue(obj));
        }
        #endregion

        #region public methods
        public string Get<T>(int id)
        {
            Type t = typeof(T);
            AddSelect(t);
            AddFrom(t);
            AddWhere(new SQLCondition("Id", "@Id"));
            StoreParameter("Id", id);
            return Query;
        }
        public string Get<T>(int id, string tableName)
        {
            Type t = typeof(T);
            AddSelect(t);
            AddFrom(tableName);
            AddWhere(new SQLCondition("Id", "@Id"));
            StoreParameter("Id", id);
            return Query;
        }
        public string Get<T>(IEnumerable<int> ids)
        {
            Type t = typeof(T);
            AddSelect(t);
            AddFrom(t);
            Append("WHERE Id IN( ");

            int i = 0;
            foreach (int id in ids)
            {
                Query = Query.Append("@Id", i.ToString(), ",");
                StoreParameter($"Id{i.ToString()}", id);
                i++;
            }
            CloseBrackets();
            return Query;
        }
        public string Get<T>(IEnumerable<int> ids, string tableName)
        {
            Type t = typeof(T);
            AddSelect(t);
            AddFrom(tableName);
            Append("WHERE Id IN( ");

            int i = 0;
            foreach (int id in ids)
            {
                Query = Query.Append("@Id", i.ToString(), ",");
                StoreParameter($"Id{i.ToString()}", id);
                i++;
            }
            CloseBrackets();
            return Query;
        }
        public string Insert<T>(T obj)
        {
            Type t = typeof(T);
            AddInsertInto(t);
            AddInsertFirstColumns(t);
            CloseBrackets();
            AddInsertValues(t);
            CloseBrackets();
            StoreParametersFrom<T>(obj);
            return Query;
        }
        public string Insert<T>(T obj, string tableName)
        {
            Type t = typeof(T);
            AddInsertInto(tableName);
            AddInsertFirstColumns(t);
            CloseBrackets();
            AddInsertValues(t);
            CloseBrackets();
            StoreParametersFrom<T>(obj);
            return Query;
        }
        public string Insert<T>(IEnumerable<T> objs)
        {
            Type t = typeof(T);
            AddInsertInto(t);
            AddInsertFirstColumns(t);
            CloseBrackets();
            AddInsertValues();
                        
            int i = 0;
            foreach (T obj in objs)
            {
                OpenBrackets();
                AddInsertValues(t, i.ToString());
                StoreParametersFrom(obj, i.ToString());
                CloseBrackets();
                Comma();
                i++;
            }
            Query = Query.Remove(Query.Length);
            return Query;
        }
        public string Insert<T>(IEnumerable<T> objs, string tableName)
        {
            Type t = typeof(T);
            AddInsertInto(tableName);
            AddInsertFirstColumns(t);
            CloseBrackets();
            AddInsertValues();

            int i = 0;
            foreach (T obj in objs)
            {
                OpenBrackets();
                AddInsertValues(t, i.ToString());
                StoreParametersFrom(obj, i.ToString());
                CloseBrackets();
                Comma();
                i++;
            }
            Query = Query.Remove(Query.Length);
            return Query;
        }
        public string Update<T>(T obj) where T : iObjModelBase
        {
            Type t = typeof(T);
            AddUpdate(t);
            AddUpdateSet(t);
            AddWhere(new SQLCondition("Id", "@Id"));
            StoreParametersFrom(obj);
            return Query;
        }
        public string Update<T>(T obj, string tableName) where T : iObjModelBase
        {
            Type t = typeof(T);
            AddUpdate(tableName);
            AddUpdateSet(t);
            AddWhere(new SQLCondition("Id", "@Id"));
            StoreParametersFrom(obj);
            return Query;
        }
        //public string Update<T>(IEnumerable<T> objs) where T : iObjModelBase
        //{
        //    Type t = typeof(T);
        //    AddUpdate(t);
        //    AddUpdateSet(t);
        //    AddWhere(new SQLCondition("Id", "@Id"));
        //    StoreParametersFrom(obj);
        //    return Query;
        //}
        //public string Update<T>(IEnumerable<T> objs, string tableName) where T : iObjModelBase
        //{
        //    Type t = typeof(T);
        //    AddUpdate(tableName);
        //    AddUpdateSet(t);
        //    AddWhere(new SQLCondition("Id", "@Id"));
        //    StoreParametersFrom(obj);
        //    return Query;
        //}
        public string Delete<T>(T obj) where T : iObjModelBase
        {
            Type t = typeof(T);
            AddDeleteFrom(t);
            AddWhere(new SQLCondition("Id", "@Id"));
            _Parameters.Add("Id", obj.Id);
            return Query;
        }
        public string Delete<T>(T obj, string tableName) where T : iObjModelBase
        {
            Type t = typeof(T);
            AddDeleteFrom(tableName);
            AddWhere(new SQLCondition("Id", "@Id"));
            _Parameters.Add("Id", obj.Id);
            return Query;
        }
        #endregion
    }

}
