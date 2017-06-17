using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using MQBStatic;
using Exceptions;

namespace QBuilder
{
    public interface iObjModelBase //BORRAME
    {
        int Id { get; }
    }

    public class SQLCondition
    {
        public SQLCondition(string leftSide, string leftAlias, string rightSide, string rightAlias, string condition, string separator)
        {
            if (leftSide == null || rightSide == null)
                throw new CustomException_StringSQLBuilder("SQLCondition: A condition must have both left and right side string.");

            leftSide = leftSide.PutAhead(leftAlias, ".");
            rightSide = rightSide.PutAhead(rightAlias, ".");

            ConditionString = string.Concat(leftSide, condition, rightSide, " ", separator, " ");
        }
        /// <summary>
        /// Condition "=", separator "".
        /// </summary>
        /// <param name="leftSide"></param>
        /// <param name="leftAlias"></param>
        /// <param name="rightSide"></param>
        /// <param name="rightAlias"></param>
        public SQLCondition(string leftSide, string leftAlias, string rightSide, string rightAlias)
        {
            if (leftSide == null || rightSide == null)
                throw new CustomException_StringSQLBuilder("SQLCondition: A condition must have both left and right side string.");

            leftSide = leftSide.PutAhead(leftAlias, ".");
            rightSide = rightSide.PutAhead(rightAlias, ".");
            ConditionString = string.Concat(leftSide, "=", rightSide, " ");
        }
        /// <summary>
        /// Condition "=", separator "".
        /// </summary>
        /// <param name="leftSide"></param>
        /// <param name="rightSide"></param>
        public SQLCondition(string leftSide, string rightSide)
        {
            if (leftSide == null || rightSide == null)
                throw new CustomException_StringSQLBuilder("SQLCondition: A condition must have both left and right side string.");

            ConditionString = string.Concat(leftSide, "=", rightSide, " ");
        }
        /// <summary>
        /// Contition "IN". Add "{leftAlias}.{leftSide} IN (", join all parametersNames strings with separator as ",", and add "@" to each parameter with replace.
        /// With open brackets AND close brackets
        /// </summary>
        /// <param name="paramsIn"></param>
        public SQLCondition(string leftSide, string leftAlias, IEnumerable<string> parametersNames)
        {
            if (leftSide == null || parametersNames == null)
                throw new CustomException_StringSQLBuilder("SQLCondition: A condition must have both left and right side string.");

            string left = leftSide;
            if (!string.IsNullOrEmpty(leftAlias)) left = left.PutAhead(leftAlias, ".");

            ConditionString = string.Join(",", parametersNames)
                .PutAhead($"{left} IN (@")
                .Append(") ");
            ConditionString = ConditionString.Replace(",", ",@");
        }

        public string ConditionString { get; set; }
    }

    public class StringSQLBuilder : MQBStatic_QBuilder
    {
        public StringSQLBuilder()
        {
            Query = "";
        }

        #region properties
        public string Columns { get; protected set; }
        public string Query { get; set; }
        #endregion

        #region helpers
        public static string GetPropertyName<T, TMember>(Expression<Func<T, TMember>> expression)
        {
            //http://stackoverflow.com/questions/273941/get-property-name-and-type-using-lambda-expression
            var member = expression.Body as MemberExpression;
            if (member != null)
                return member.Member.Name;

            throw new CustomException_StringSQLBuilder($"QBuilder.GetPropertyName: Expression {expression} is not a member access");
        }
        public static string GetPropertyName<T, TMember>(Expression<Func<T, TMember>> expression, string tableAlias)
        {
            //http://stackoverflow.com/questions/273941/get-property-name-and-type-using-lambda-expression
            var member = expression.Body as MemberExpression;
            if (member != null)
                return member.Member.Name.PutAhead(tableAlias, ".");

            throw new CustomException_StringSQLBuilder($"QBuilder.GetPropertyName: Expression {expression} is not a member access");
        }
        public static string AddTableAlias(string column, string tableAlias)
        {
            return column.PutAhead(tableAlias, ".");
        }
        public static string RemoveFieldsUnderscore(string fieldName)
        {
            if (fieldName.StartsWith("_")) return fieldName.Remove(0, 1);
            return fieldName;
        }
        protected void RemoveId()
        {
            Columns = Columns.RemoveOrThis(Columns.IndexOf("Id,"), 3);
        }
        protected string RemoveIdFrom(string columns)
        {
            return columns.RemoveOrThis(columns.IndexOf("Id,"), 3);
        }
        protected void AddTableAliasToColumns(string tableAlias)
        {
            Columns = Columns.Replace(",", "," + tableAlias + ".");
            Columns = Columns.PutAhead(tableAlias + ".");
        }
        protected void RemoveLastComma()
        {
            Columns = Columns.Remove(Columns.LastIndexOf(','));
        }
        protected void MakeColumnsParameters()
        {
            Columns = Columns.PutAhead("@");
            Columns = Columns.Replace(",", ",@");
        }
        protected string MakeParameters(string strs)
        {
            string columns = strs.PutAhead("@");
            columns = Columns.Replace(",", ",@");
            return columns;
        }
        #endregion

        #region builder sub-classes
        public sealed class StringBuilder
        {
            #region get columns by string[]
            /// <summary>
            /// Without first and last commas.
            /// </summary>
            /// <param name="strs"></param>
            /// <param name="tableAlias"></param>
            /// <returns></returns>
            public string ConcatAndAddCommasAndAlias(IEnumerable<string> strs, string tableAlias)
            {
                strs = strs.OrderBy(x => x);
                string result = string.Join("," + tableAlias + ".", strs);
                result = result.PutAhead(tableAlias + ".");
                return result;
            }
            /// <summary>
            /// Without first and last commas. columnsAlias must have same Count as strs.
            /// </summary>
            /// <param name="strs"></param>
            /// <param name="tableAlias"></param>
            /// <param name="columnsAlias"></param>
            /// <returns></returns>
            public string ConcatAndAddCommasAndAlias(IEnumerable<string> strs, string tableAlias, IEnumerable<string> columnsAlias)
            {
                strs = strs.Zip(columnsAlias, (str, cAlias) => str + " " + cAlias).OrderBy(x => x);
                string result = string.Join(",", strs);
                result = result.Replace(",", "," + tableAlias + ".");
                result = result.PutAhead(tableAlias + ".");

                return result;
            }
            #endregion
        }
        public sealed class ColumnsBuilder
        {
            public ColumnsBuilder(StringSQLBuilder qBuilder)
            {
                _qBuilder = qBuilder;
            }

            private StringSQLBuilder _qBuilder;

            #region get columns by type
            /// <summary>
            /// Without first and last commas.
            /// </summary>
            /// <param name="t"></param>
            /// <returns></returns>
            public void GetAllColumns(Type t)
            {
                _qBuilder.Columns = _Columns[t];
            }
            /// <summary>
            /// Without first and last commas.
            /// </summary>
            /// <param name="t"></param>
            /// <param name="tableAlias"></param>
            /// <param name="columnsAlias"></param>
            /// <returns></returns>
            public void GetAllColumns(Type t, string tableAlias)
            {
                _qBuilder.Columns = _Columns[t];
                _qBuilder.AddTableAliasToColumns(tableAlias);
            }
            /// <summary>
            /// Without first and last commas.
            /// Properties and fields of t order: First properties and then fields; both ordered by name membersArray.OrderBy(x => x.Name).
            /// Also columnsAlias must have same number of strings as settable properties + fields.
            /// </summary>
            /// <param name="t"></param>
            /// <param name="columnsAlias"></param>
            public void GetAllColumns(Type t, IEnumerable<string> columnsAlias)
            {
                IEnumerable<string> names = _NamesList[t]
                    .Select(x => RemoveFieldsUnderscore(x))
                    .OrderBy(x => x);

                names = names.Zip(columnsAlias, (name, cAlias) => RemoveFieldsUnderscore(name) + " " + cAlias);
                _qBuilder.Columns = string.Join(",", names);
            }
            /// <summary>
            /// Without first and last commas.
            /// Properties and fields of t order: First properties and then fields; both ordered by name membersArray.OrderBy(x => x.Name).
            /// Also columnsAlias must have same number of strings as settable properties + fields.
            /// </summary>
            /// <param name="t"></param>
            /// <param name="columnsAlias"></param>
            /// <param name="tableAlias"></param>
            /// <returns></returns>
            public void GetAllColumns(Type t, string tableAlias, IEnumerable<string> columnsAlias)
            {
                IEnumerable<string> names = _NamesList[t]
                    .Select(x => RemoveFieldsUnderscore(x))
                    .OrderBy(x => x);

                names = names.Zip(columnsAlias, (name, cAlias) => tableAlias + "." + name + " " + cAlias);
                _qBuilder.Columns = string.Join(",", names);
            }
            /// <summary>
            /// Without first and last commas.
            /// </summary>
            /// <param name="t"></param>
            /// <returns></returns>
            public void GetAllColumnsWOId(Type t)
            {
                _qBuilder.Columns = _Columns[t];
                _qBuilder.RemoveId();
            }
            /// <summary>
            /// Without first and last commas.
            /// </summary>
            /// <param name="t"></param>
            /// <param name="tableAlias"></param>
            /// <returns></returns>
            public void GetAllColumnsWOId(Type t, string tableAlias)
            {
                _qBuilder.Columns = _Columns[t];
                _qBuilder.RemoveId();
                _qBuilder.AddTableAliasToColumns(tableAlias);
            }
            /// <summary>
            /// Without first and last commas.
            /// Properties and fields of t order: First properties and then fields; both ordered by name membersArray.OrderBy(x => x.Name).
            /// Also columnsAlias must have same number of strings as settable properties + fields.
            /// </summary>
            /// <param name="t"></param>
            /// <param name="columnsAlias"></param>
            public void GetAllColumnsWOId(Type t, IEnumerable<string> columnsAlias)
            {
                IEnumerable<string> names = _NamesList[t]
                    .Except(new string[] { "_Id", "_id" })
                    .Select(x => RemoveFieldsUnderscore(x))
                    .OrderBy(x => x);

                names = names.Zip(columnsAlias, (name, cAlias) => RemoveFieldsUnderscore(name) + " " + cAlias);
                _qBuilder.Columns = string.Join(",", names);
            }
            /// <summary>
            /// Without first and last commas.
            /// Properties and fields of t order: First properties and then fields; both ordered by name membersArray.OrderBy(x => x.Name).
            /// Also columnsAlias must have same number of strings as settable properties + fields.
            /// </summary>
            /// <param name="t"></param>
            /// <param name="tableAlias"></param>
            /// <param name="columnsAlias"></param>
            /// <returns></returns>
            public void GetAllColumnsWOId(Type t, string tableAlias, IEnumerable<string> columnsAlias)
            {
                IEnumerable<string> names = _NamesList[t]
                    .Except(new string[] { "_Id", "_id" })
                    .Select(x => RemoveFieldsUnderscore(x))
                    .OrderBy(x => x);

                names = names.Zip(columnsAlias, (name, cAlias) => tableAlias + "." + RemoveFieldsUnderscore(name) + " " + cAlias);
                _qBuilder.Columns = string.Join(",", names);
            }
            #endregion
        }
        public sealed class InsertBuilder
        {
            public InsertBuilder(StringSQLBuilder qBuilder)
            {
                _qBuilder = qBuilder;
            }

            private StringSQLBuilder _qBuilder;

            /// <summary>
            /// Without first and last commas.
            /// </summary>
            /// <param name="t"></param>
            /// <returns></returns>
            public string GetInsertValuesString(Type t)
            {
                string values = _Columns[t];
                values = _qBuilder.RemoveIdFrom(values);
                values = values.Replace(",", "," + "@");
                values = values.Remove(values.LastIndexOf(",@"), 2);
                return values;
            }
            /// <summary>
            /// Without first and last commas.
            /// </summary>
            /// <param name="t"></param>
            /// <param name="paramSuffix"></param>
            /// <returns></returns>
            public string GetInsertValuesString(Type t, string paramSuffix)
            {
                string values = _Columns[t];
                values = _qBuilder.RemoveIdFrom(values);
                values = values.Replace(",", paramSuffix + "," + "@");
                values = values.Remove(values.LastIndexOf(",@"), 2);
                return values;
            }
            /// <summary>
            /// Without first and last commas.
            /// </summary>
            /// <param name="strs"></param>
            /// <param name="paramSuffix"></param>
            /// <returns></returns>
            public string GetInsertValuesString(IEnumerable<string> strs, string paramSuffix)
            {
                string values = "";

                foreach (string str in strs) values = values.Append("@", str, paramSuffix, ",");

                values = values.Remove(values.LastIndexOf(','))
                    .RemoveOrThis(values.LastIndexOf("@Id,"), 4 + paramSuffix.Length);

                return values;
            }
        }
        public sealed class UpdateBuilder
        {
            /// <summary>
            /// Without first and last commas.
            /// </summary>
            /// <param name="t"></param>
            /// <param name="paramSuffix"></param>
            /// <returns></returns>
            public string GetUpdateSetString(Type t, string paramSuffix)
            {
                IEnumerable<string> names = _NamesList[t]
                    .Except(new string[] { "_Id", "_id" })
                    .Select(x => RemoveFieldsUnderscore(x))
                    .OrderBy(x => x);
                string set = "";

                foreach (string name in names) set = set.Append(name, "=@", name, paramSuffix, ",");
                set = set.Remove(set.LastIndexOf(','));

                return set;
            }
            /// <summary>
            /// Without first and last commas.
            /// </summary>
            /// <param name="t"></param>
            /// <param name="tableAlias"></param>
            /// <param name="paramSuffix"></param>
            /// <returns></returns>
            public string GetUpdateSetString(Type t, string tableAlias, string paramSuffix)
            {
                IEnumerable<string> names = _NamesList[t]
                    .Except(new string[] { "_Id", "_id" })
                    .Select(x => RemoveFieldsUnderscore(x))
                    .OrderBy(x => x);
                string set = "";

                foreach (string name in names) set = set.Append(tableAlias, ".", name, "=@", name, paramSuffix, ",");
                set = set.Remove(set.LastIndexOf(','));

                return set;
            }
            /// <summary>
            /// Without first and last commas.
            /// </summary>
            /// <param name="strs"></param>
            /// <param name="paramSuffix"></param>
            /// <returns></returns>
            public string GetUpdateSetString(IEnumerable<string> strs, string paramSuffix)
            {
                strs = strs
                    .Except(new string[] { "_Id", "_id", "Id", "id" })
                    .Select(x => RemoveFieldsUnderscore(x))
                    .OrderBy(x => x);
                string set = "";

                foreach (string str in strs) set = set.Append(str, "=@", str, paramSuffix, ",");

                set = set.Remove(set.LastIndexOf(','));
                return set;
            }
            /// <summary>
            /// Without first and last commas.
            /// </summary>
            /// <param name="strs"></param>
            /// <param name="tableAlias"></param>
            /// <param name="paramSuffix"></param>
            /// <returns></returns>
            public string GetUpdateSetString(IEnumerable<string> strs, string tableAlias, string paramSuffix)
            {
                strs = strs
                    .Except(new string[] { "_Id", "_id", "Id", "id" })
                    .Select(x => RemoveFieldsUnderscore(x))
                    .OrderBy(x => x);
                string set = "";

                foreach (string str in strs) set = set.Append(tableAlias, ".", str, "=@", str, paramSuffix, ",");

                set = set.Remove(set.LastIndexOf(','));
                return set;
            }
        }
        #endregion

        #region SELECT 
        public StringSQLBuilder AddSelect(Type t)
        {
            var cBuilder = new ColumnsBuilder(this);
            cBuilder.GetAllColumns(t);
            Query = $"SELECT {Columns} ";
            return this;
        }
        public StringSQLBuilder AddSelect(Type t, string tableAlias)
        {
            var cBuilder = new ColumnsBuilder(this);
            cBuilder.GetAllColumns(t, tableAlias);
            Query = $"SELECT {Columns} ";
            return this;
        }
        public StringSQLBuilder AddSelect(Type t, string tableAlias, IEnumerable<string> columnsAlias)
        {
            var cBuilder = new ColumnsBuilder(this);
            cBuilder.GetAllColumns(t, tableAlias, columnsAlias);
            Query = $"SELECT {Columns} ";
            return this;
        }
        public StringSQLBuilder AddSelect(IEnumerable<string> columns)
        {
            Query = $"SELECT {string.Join(",", columns)} ";
            return this;
        }
        public StringSQLBuilder AddSelect(IEnumerable<string> columns, string tableAlias)
        {
            var cBuilder = new StringBuilder();
            Query = $"SELECT {cBuilder.ConcatAndAddCommasAndAlias(columns, tableAlias)} ";
            return this;
        }
        public StringSQLBuilder AddSelect(IEnumerable<string> columns, string tableAlias, IEnumerable<string> columnsAlias)
        {
            var cBuilder = new StringBuilder();
            Query = $"SELECT {cBuilder.ConcatAndAddCommasAndAlias(columns, tableAlias, columnsAlias)} ";
            return this;
        }
        public StringSQLBuilder AddSelectColumns(Type t)
        {
            var cBuilder = new ColumnsBuilder(this);
            cBuilder.GetAllColumns(t);
            Query = Query.Append(",", Columns, " ");
            return this;
        }
        public StringSQLBuilder AddSelectColumns(Type t, string tableAlias)
        {
            var cBuilder = new ColumnsBuilder(this);
            cBuilder.GetAllColumns(t, tableAlias);
            Query = Query.Append(",", Columns, " ");
            return this;
        }
        public StringSQLBuilder AddSelectColumns(Type t, string tableAlias, IEnumerable<string> columnsAlias)
        {
            var cBuilder = new ColumnsBuilder(this);
            cBuilder.GetAllColumns(t, tableAlias, columnsAlias);
            Query = Query.Append(",", Columns, " ");
            return this;
        }
        public StringSQLBuilder AddSelectColumns(IEnumerable<string> columns, string tableAlias, IEnumerable<string> columnsAlias)
        {
            var cBuilder = new StringBuilder();
            Query = Query.Append(",", cBuilder.ConcatAndAddCommasAndAlias(columns, tableAlias, columnsAlias), " ");
            return this;
        }
        #endregion
        #region INSERT
        //TODO: ¿INSERT mal?, se puede insertar en varias tablas, ejemplo: http://stackoverflow.com/questions/452859/inserting-multiple-rows-in-a-single-sql-query
        public StringSQLBuilder AddInsertInto()
        {
            Query = "INSERT INTO ";
            return this;
        }
        /// <summary>
        /// Without columns, only "INSERT INTO " and table with alias.
        /// </summary>
        /// <param name="ownersIds"></param>
        /// <param name="tableName"></param>
        /// <param name="tableAlias"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInsertInto(string tableName, string tableAlias = "")
        {
            Query = string.Concat("INSERT INTO ", tableName, " ", tableAlias, " ");
            return this;
        }
        /// <summary>
        /// Without columns, only "INSERT INTO " and table with alias.
        /// Name of table = name of type t.
        /// </summary>
        /// <param name="ownersIds"></param>
        /// <param name="t"></param>
        /// <param name="tableAlias"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInsertInto(Type t, string tableAlias = "")
        {
            Query = string.Concat("INSERT INTO ", t.Name, " ", tableAlias, " ");
            return this;
        }
        /// <summary>
        /// With open brackets. Without last comma.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInsertFirstColumns(Type t)
        {
            var cBuilder = new ColumnsBuilder(this);
            cBuilder.GetAllColumnsWOId(t);
            Query = Query.Append("(", Columns);
            return this;
        }
        /// <summary>
        /// With open brackets. Without last comma.
        /// </summary>
        /// <param name="columns"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInsertFirstColumns(IEnumerable<string> columns)
        {
            Query = Query.Append("(", string.Join(",", columns));
            return this;
        }
        /// <summary>
        /// With open brackets. Without last comma.
        /// </summary>
        /// <param name="columns"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInsertFirstColumns(IEnumerable<string> columns, string tableAlias)
        {
            var cBuilder = new StringBuilder();
            Query = Query.Append("(", cBuilder.ConcatAndAddCommasAndAlias(columns, tableAlias));
            return this;
        }
        /// <summary>
        /// Without last and first commas and without both brackets.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInsertColumns(Type t)
        {
            var cBuilder = new ColumnsBuilder(this);
            cBuilder.GetAllColumnsWOId(t);
            Query = Query.Append(Columns);
            return this;
        }
        /// <summary>
        /// Without last and first commas and without both brackets.
        /// </summary>
        /// <param name="columns"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInsertColumns(IEnumerable<string> columns)
        {
            var cBuilder = new ColumnsBuilder(this);
            Query = Query.Append(string.Join(",", columns));
            return this;
        }
        /// <summary>
        /// Only add "VALUES " + bracket.
        /// </summary>
        /// <returns></returns>
        public StringSQLBuilder AddInsertValues(string bracket = "")
        {
            Query = Query.Append("VALUES ", bracket);
            return this;
        }
        /// <summary>
        /// Does not add "VALUES (". Always without close brackets and without last comma.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="paramSuffix"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInsertValues(Type t, string paramSuffix = "")
        {
            var iBuilder = new InsertBuilder(this);
            Query = Query.Append(iBuilder.GetInsertValuesString(t, paramSuffix));
            return this;
        }
        /// <summary>
        /// Does not add "VALUES (". Always without close brackets and without last comma.
        /// </summary>
        /// <param name="columns"></param>
        /// <param name="paramSuffix"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInsertValues(IEnumerable<string> columns, string paramSuffix = "")
        {
            var iBuilder = new InsertBuilder(this);
            Query = Query.Append(iBuilder.GetInsertValuesString(columns, paramSuffix));
            return this;
        }
        #endregion
        #region UPDATE
        public StringSQLBuilder AddUpdate(string tableName, string tableAlias = "")
        {
            Query = string.Concat("UPDATE ", tableName, " ", tableAlias, " ");
            return this;
        }
        /// <summary>
        /// Name of table = name of type t
        /// </summary>
        /// <param name="ownersIds"></param>
        /// <param name="t"></param>
        /// <param name="tableAlias"></param>
        /// <returns></returns>
        public StringSQLBuilder AddUpdate(Type t, string tableAlias = "")
        {
            Query = string.Concat("UPDATE ", t.Name, " ", tableAlias, " ");
            return this;
        }
        public StringSQLBuilder AddUpdateTable(string tableName, string tableAlias = "")
        {
            Query = Query.Append(",", tableName, " ", tableAlias, " ");
            return this;
        }
        /// <summary>
        /// Name of table = name of type t
        /// </summary>
        /// <param name="ownersIds"></param>
        /// <param name="t"></param>
        /// <param name="tableAlias"></param>
        /// <returns></returns>
        public StringSQLBuilder AddUpdateTable(Type t, string tableAlias = "")
        {
            Query = Query.Append(",", t.Name, " ", tableAlias, " ");
            return this;
        }
        public StringSQLBuilder AddUpdateSet(Type t, string paramSuffix = "")
        {
            var uBuilder = new UpdateBuilder();
            Query = Query.Append("SET ", uBuilder.GetUpdateSetString(t, paramSuffix), " ");
            return this;
        }
        /// <summary>
        /// paramSuffix can be empty string, tableAlias can not.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="tableAlias"></param>
        /// <param name="paramSuffix"></param>
        /// <returns></returns>
        public StringSQLBuilder AddUpdateSet(Type t, string tableAlias, string paramSuffix)
        {
            var uBuilder = new UpdateBuilder();
            Query = Query.Append("SET ", uBuilder.GetUpdateSetString(t, tableAlias, paramSuffix), " ");
            return this;
        }
        public StringSQLBuilder AddUpdateSet(IEnumerable<string> columns, string paramSuffix = "")
        {
            var uBuilder = new UpdateBuilder();
            Query = Query.Append("SET ", uBuilder.GetUpdateSetString(columns, paramSuffix), " ");
            return this;
        }
        /// <summary>
        /// paramSuffix can be empty string, tableAlias can not.
        /// </summary>
        /// <param name="columns"></param>
        /// <param name="tableAlias"></param>
        /// <param name="paramSuffix"></param>
        /// <returns></returns>
        public StringSQLBuilder AddUpdateSet(IEnumerable<string> columns, string tableAlias, string paramSuffix)
        {
            var uBuilder = new UpdateBuilder();
            Query = Query.Append("SET ", uBuilder.GetUpdateSetString(columns, tableAlias, paramSuffix), " ");
            return this;
        }
        #endregion
        #region DELETE
        /// <summary>
        /// Only add "DELETE ".
        /// </summary>
        /// <returns></returns>
        public StringSQLBuilder AddDelete()
        {
            Query = "DELETE ";
            return this;
        }
        public StringSQLBuilder AddDelete(IEnumerable<string> columns)
        {
            Query = string.Concat("DELETE ", string.Join(",", columns), " ");
            return this;
        }
        public StringSQLBuilder AddDelete(IEnumerable<string> columns, string tableAlias)
        {
            var cBuilder = new StringBuilder();
            Query = string.Concat("DELETE ", cBuilder.ConcatAndAddCommasAndAlias(columns, tableAlias), " ");
            return this;
        }
        public StringSQLBuilder AddDeleteFrom(string tableName, string tableAlias = "")
        {
            Query = Query.Append("DELETE FROM ", tableName, " ", tableAlias, " ");
            return this;
        }
        /// <summary>
        /// Name of table = name of type t.
        /// </summary>
        /// <param name="ownersIds"></param>
        /// <param name="t"></param>
        /// <param name="tableAlias"></param>
        /// <returns></returns>
        public StringSQLBuilder AddDeleteFrom(Type t, string tableAlias = "")
        {
            Query = Query.Append("DELETE FROM ", t.Name, " ", tableAlias, " ");
            return this;
        }
        #endregion
        #region CLAUSES
        public StringSQLBuilder AddFrom(string tableName, string alias = "")
        {
            Query = Query.Append("FROM ", tableName, " ", alias, " ");
            return this;
        }
        /// <summary>
        /// Name of table = name of type t.
        /// </summary>
        /// <param name="ownersIds"></param>
        /// <param name="t"></param>
        /// <param name="alias"></param>
        /// <returns></returns>
        public StringSQLBuilder AddFrom(Type t, string alias = "")
        {
            Query = Query.Append("FROM ", t.Name, " ", alias, " ");
            return this;
        }
        /// <summary>
        /// SOLO añade "IdOwner=Id AND IdOwner=Id", no WHERE u otra cosa.
        /// El alias de la tabla que tiene la id de comunidad debe ser "ocdad", y el de la tabla del ejercicio "oejer".
        /// </summary>
        /// <param name="ownersIds"></param>
        /// <returns></returns>
        public StringSQLBuilder AddOwnersClauses(IEnumerable<int> ownersIds, string tableAlias = "")
        {
            tableAlias = string.IsNullOrEmpty(tableAlias) ? "" : $"{tableAlias}.";
            int[] ids = ownersIds.ToArray();
            Query = Query.Append($"{tableAlias}IdOwnerComunidad=", ids[0].ToString(), $" AND {tableAlias}IdOwnerEjercicio=", ids[1].ToString(), " ");
            return this;
        }
        /// <summary>
        /// Add only WHERE.
        /// </summary>
        /// <returns></returns>
        public StringSQLBuilder AddWhere()
        {
            Query = Query.Append("WHERE ");
            return this;
        }
        public StringSQLBuilder AddWhere(IEnumerable<SQLCondition> conditions)
        {
            Query = Query.Append("WHERE ");
            foreach (SQLCondition condition in conditions) Query = Query.Append(condition.ConditionString, " ");
            return this;
        }
        public StringSQLBuilder AddWhere(SQLCondition condition)
        {
            Query = Query.Append("WHERE ", condition.ConditionString, " ");
            return this;
        }
        public StringSQLBuilder AddJoin(string typeOfJoin, string tableName, string alias = "")
        {
            Query = Query.Append(typeOfJoin, " JOIN ", tableName, " ", alias, " ");
            return this;
        }
        /// <summary>
        /// Name of table = name of type t.
        /// </summary>
        /// <param name="typeOfJoin"></param>
        /// <param name="ownersIds"></param>
        /// <param name="t"></param>
        /// <param name="alias"></param>
        /// <returns></returns>
        public StringSQLBuilder AddJoin(string typeOfJoin, Type t, string alias = "")
        {
            Query = Query.Append(typeOfJoin, " JOIN ", t.Name, " ", alias, " ");
            return this;
        }
        public StringSQLBuilder AddOn(IEnumerable<SQLCondition> conditions)
        {
            Query = Query.Append("ON ");
            foreach (SQLCondition condition in conditions) Query = Query.Append(condition.ConditionString);
            return this;
        }
        public StringSQLBuilder AddOn(SQLCondition condition)
        {
            Query = Query.Append("ON ", condition.ConditionString);
            return this;
        }
        public StringSQLBuilder AddOrderBy(IEnumerable<string> columns)
        {
            var cBuilder = new StringBuilder();
            Query = Query.Append("ORDER BY ", string.Join(",", columns), " ");
            return this;
        }
        public StringSQLBuilder AddOrderBy(IEnumerable<string> columns, string alias)
        {
            var cBuilder = new StringBuilder();
            Query = Query.Append("ORDER BY ", cBuilder.ConcatAndAddCommasAndAlias(columns, alias), " ");
            return this;
        }
        /// <summary>
        /// With open brackets, WITHOUT close brackets
        /// </summary>
        /// <param name="t"></param>
        /// <param name="tableAlias"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInColumns(Type t)
        {
            var cBuilder = new ColumnsBuilder(this);
            cBuilder.GetAllColumns(t);
            Query = Query.Append("IN (", Columns);
            return this;
        }
        /// <summary>
        /// With open brackets, WITHOUT close brackets
        /// </summary>
        /// <param name="t"></param>
        /// <param name="tableAlias"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInColumns(Type t, string tableAlias)
        {
            var cBuilder = new ColumnsBuilder(this);
            cBuilder.GetAllColumns(t, tableAlias);
            Query = Query.Append("IN (", Columns);
            return this;
        }
        /// <summary>
        /// With open brackets, WITHOUT close brackets
        /// </summary>
        /// <param name="columns"></param>
        /// <param name="tableAlias"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInColumns(IEnumerable<string> columns)
        {
            Query = Query.Append("IN (", string.Join(",", columns));
            return this;
        }
        /// <summary>
        /// With open brackets, WITHOUT close brackets
        /// </summary>
        /// <param name="columns"></param>
        /// <param name="tableAlias"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInColumns(IEnumerable<string> columns, string tableAlias)
        {
            var cBuilder = new StringBuilder();
            Query = Query.Append("IN (", cBuilder.ConcatAndAddCommasAndAlias(columns, tableAlias));
            return this;
        }
        /// <summary>
        /// With open brackets, WITHOUT close brackets
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInParameters(Type t)
        {
            var cBuilder = new ColumnsBuilder(this);
            cBuilder.GetAllColumns(t);
            MakeColumnsParameters();
            Query = Query.Append("IN (", Columns);
            return this;
        }
        /// <summary>
        /// Add "IN (", join all parametersNames strings with separator as ",", and add "@" to each parameter with replace.
        /// With open brackets, WITHOUT close brackets
        /// </summary>
        /// <param name="parametersNames"></param>
        /// <returns></returns>
        public StringSQLBuilder AddInParameters(IEnumerable<string> parametersNames)
        {
            Query = Query.Append(
                "IN (",
                MakeParameters(
                    string.Join(",", parametersNames)));
            return this;
        }
        /// <summary>
        /// Add "{firstSeparator} condition". With in-between space.
        /// </summary>
        /// <param name="firstSeparator"></param>
        /// <param name="condition"></param>
        /// <returns></returns>
        public StringSQLBuilder AddCondition(string firstSeparator, SQLCondition condition)
        {
            Query = Query.Append(firstSeparator, " ", condition.ConditionString);

            return this;
        }
        /// <summary>
        /// Add first firstSeparator, then the conditions. Once it have begun to add conditions, it won't add separators before, 
        /// between or after the conditions (of course conditions can have their self separators, as always).
        /// </summary>
        /// <param name="conditions"></param>
        /// <returns></returns>
        public StringSQLBuilder AddConditions(string firstSeparator, IEnumerable<SQLCondition> conditions)
        {
            Query = Query.Append(firstSeparator);
            foreach (SQLCondition condition in conditions)
                Query = Query.Append(condition.ConditionString);

            return this;
        }
        #endregion
        #region others
        public StringSQLBuilder Append(string str)
        {
            Query = Query.Append(str);
            return this;
        }
        public StringSQLBuilder Append(IEnumerable<string> strings)
        {
            Query = Query.Append(strings.ToArray());
            return this;
        }
        public StringSQLBuilder AddTable(string tableName, string alias = "")
        {
            Query = Query.Append(tableName, " ", alias, " ");
            return this;
        }
        public StringSQLBuilder AddTable(Type t, string alias = "")
        {
            Query = Query.Append(t.Name, " ", alias, " ");
            return this;
        }
        public StringSQLBuilder Comma()
        {
            Query = Query.Append(",");
            return this;
        }
        public StringSQLBuilder SemiColon()
        {
            Query = Query.Append(";");
            return this;
        }
        public StringSQLBuilder OpenBrackets()
        {
            Query = Query.Append(" (");
            return this;
        }
        public StringSQLBuilder CloseBrackets()
        {
            Query = Query.Append(") ");
            return this;
        }
        #endregion
    }
}
