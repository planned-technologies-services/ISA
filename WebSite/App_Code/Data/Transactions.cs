using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;
using System.Xml;
using System.Xml.XPath;
using System.Web;
using System.Web.Caching;
using System.Web.Configuration;
using System.Web.Security;

namespace Planned.Data
{
	public class TransactionManager
    {
        
        private string _transaction;
        
        private string _status;
        
        private SortedDictionary<string, DataTable> _tables;
        
        private SortedDictionary<string, object> _primaryKeys;
        
        private List<ActionArgs> _arguments;
        
        public TransactionManager(string transaction)
        {
            _transaction = transaction;
            _tables = new SortedDictionary<string, DataTable>();
            _primaryKeys = new SortedDictionary<string, object>();
            _arguments = new List<ActionArgs>();
        }
        
        public string Transaction
        {
            get
            {
                return _transaction;
            }
            set
            {
                _transaction = value;
            }
        }
        
        public string Status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;
            }
        }
        
        public SortedDictionary<string, DataTable> Tables
        {
            get
            {
                return _tables;
            }
        }
        
        public SortedDictionary<string, object> PrimaryKeys
        {
            get
            {
                return _primaryKeys;
            }
        }
        
        public List<ActionArgs> Arguments
        {
            get
            {
                return _arguments;
            }
        }
        
        public DataTable GetTable(string controller, DbCommand command)
        {
            DataTable t = null;
            if (!(Tables.TryGetValue(controller, out t)))
            {
                DbParameter p = command.Parameters["@PageRangeLastRowNumber"];
                if (p != null)
                	p.Value = 500;
                t = new DataTable(controller);
                t.Load(command.ExecuteReader());
                foreach (DataColumn c in t.Columns)
                	c.AllowDBNull = true;
                Tables.Add(controller, t);
            }
            return t;
        }
        
        public void Delete()
        {
            HttpContext.Current.Session.Remove(("TransactionManager_" + _transaction));
        }
        
        public static TransactionManager Create(string transaction)
        {
            if (String.IsNullOrEmpty(transaction))
            	return null;
            string[] transactionInfo = transaction.Split(':');
            transaction = transactionInfo[0];
            string key = ("TransactionManager_" + transaction);
            TransactionManager tm = ((TransactionManager)(HttpContext.Current.Session[key]));
            if (tm == null)
            {
                tm = new TransactionManager(transaction);
                HttpContext.Current.Session[key] = tm;
            }
            if (transactionInfo.Length == 2)
            	tm._status = transactionInfo[1];
            if (tm.Status == "abort")
            {
                tm.Delete();
                return null;
            }
            return tm;
        }
        
        private static bool RowIsMatched(PageRequest request, ViewPage page, DataRow row)
        {
            foreach (string f in request.Filter)
            {
                Match m = Regex.Match(f, "^(?\'FieldName\'\\w+)\\:(?\'Operation\'=)(?\'Value\'.+)$");
                if (m.Success)
                {
                    string fieldName = m.Groups["FieldName"].Value;
                    if (page.ContainsField(fieldName))
                    {
                        string fieldValue = m.Groups["Value"].Value;
                        if (fieldValue == "null")
                        	fieldValue = String.Empty;
                        object fv = row[fieldName];
                        if (!((Convert.ToString(fv) == fieldValue)))
                        	return false;
                    }
                }
            }
            return true;
        }
        
        public static DbDataReader ExecuteReader(PageRequest request, ViewPage page, DbCommand command)
        {
            TransactionManager tm = Create(request.Transaction);
            if (tm == null)
            	return command.ExecuteReader();
            DataTable t = tm.GetTable(request.Controller, command);
            t.DefaultView.Sort = request.SortExpression;
            DataTable t2 = t.Clone();
            int rowsToSkip = (page.PageIndex * page.PageSize);
            foreach (DataRowView r in t.DefaultView)
            	if (RowIsMatched(request, page, r.Row))
                	if (rowsToSkip > 0)
                    	rowsToSkip++;
                    else
                    {
                        DataRow r2 = t2.NewRow();
                        foreach (DataColumn c in t.Columns)
                        	r2[c.ColumnName] = r[c.ColumnName];
                        t2.Rows.Add(r2);
                        if (t2.Rows.Count == page.PageSize)
                        	break;
                    }
            if (page.RequiresRowCount)
            {
                int totalRowCount = 0;
                foreach (DataRowView r in t.DefaultView)
                	if (RowIsMatched(request, page, r.Row))
                    	totalRowCount++;
                page.TotalRowCount = totalRowCount;
            }
            return new DataTableReader(t2);
        }
        
        public static int ExecuteNonQuery(ActionArgs args, ActionResult result, ViewPage page, DbCommand command)
        {
            TransactionManager tm = Create(args.Transaction);
            if (tm == null)
            	return command.ExecuteNonQuery();
            else
            	if (tm.Status == "complete")
                	return command.ExecuteNonQuery();
            int rowsAffected = tm.ExecuteAction(args, result, page);
            tm.Arguments.Add(args);
            return rowsAffected;
        }
        
        public static void Complete(ActionArgs args, ActionResult result, ViewPage page)
        {
            TransactionManager tm = Create(args.Transaction);
            if ((tm != null) && (tm.Status == "complete"))
            	tm.Complete(result);
        }
        
        public static bool InTransaction(ActionArgs args)
        {
            return (!(String.IsNullOrEmpty(args.Transaction)) && !(args.Transaction.EndsWith(":complete")));
        }
        
        public void Complete(ActionResult result)
        {
            IDataController controller = ControllerFactory.CreateDataController();
            SortedDictionary<string, object> keys = new SortedDictionary<string, object>();
            foreach (ActionArgs args in Arguments)
            {
                args.Transaction = null;
                // resolve foreign keys
                foreach (FieldValue v in result.Values)
                {
                    FieldValue v2 = args.SelectFieldValueObject(v.Name);
                    if (v2 == null)
                    {
                        List<FieldValue> values = new List<FieldValue>(args.Values);
                        values.Add(new FieldValue(v.Name, v.Value));
                        args.Values = values.ToArray();
                    }
                    else
                    {
                        v2.NewValue = v.Value;
                        v2.Modified = true;
                    }
                }
                // resolve virtual primary keys
                if ((args.CommandName == "Update") || (args.CommandName == "Delete"))
                	foreach (FieldValue v in args.Values)
                    {
                        object key = null;
                        if (keys.TryGetValue(String.Format("{0}:{1}:{2}", args.Controller, v.Name, v.Value), out key))
                        	v.OldValue = key;
                    }
                // execute an actual update and raise exception if errors detected
                SortedDictionary<string, object> argValues = new SortedDictionary<string, object>();
                if (args.CommandName == "Insert")
                	foreach (FieldValue v in args.Values)
                    	argValues.Add(v.Name, v.Value);
                ActionResult r = controller.Execute(args.Controller, args.View, args);
                r.RaiseExceptionIfErrors();
                // register physical primary keys
                if (args.CommandName == "Insert")
                	foreach (FieldValue v in r.Values)
                    	if (argValues.ContainsKey(v.Name))
                        	keys.Add(String.Format("{0}:{1}:{2}", args.Controller, v.Name, argValues[v.Name]), v.NewValue);
            }
        }
        
        public int ExecuteAction(ActionArgs args, ActionResult result, ViewPage page)
        {
            DataTable t = GetTable(args.Controller, null);
            if (args.CommandName == "Insert")
            {
                DataRow r = t.NewRow();
                foreach (FieldValue v in args.Values)
                {
                    DataField f = page.FindField(v.Name);
                    if (f.IsPrimaryKey && f.ReadOnly)
                    {
                        object key = null;
                        if (f.Type == "Guid")
                        	key = Guid.NewGuid();
                        else
                        	if (!(PrimaryKeys.TryGetValue(args.Controller, out key)))
                            {
                                key = -1;
                                PrimaryKeys.Add(args.Controller, key);
                            }
                            else
                            {
                                key = (Convert.ToInt32(key) - 1);
                                PrimaryKeys[args.Controller] = key;
                            }
                        r[v.Name] = key;
                        result.Values.Add(new FieldValue(v.Name, key));
                        FieldValue fv = args.SelectFieldValueObject(v.Name);
                        fv.NewValue = key;
                        fv.Modified = true;
                    }
                    else
                    	if (v.Modified)
                        	if (v.NewValue == null)
                            	r[v.Name] = DBNull.Value;
                            else
                            	r[v.Name] = v.NewValue;
                }
                t.Rows.Add(r);
                return 1;
            }
            else
            {
                DataRow targetRow = null;
                foreach (DataRow r in t.Rows)
                {
                    bool matched = true;
                    foreach (DataField f in page.Fields)
                    	if (f.IsPrimaryKey)
                        {
                            object kv = r[f.Name];
                            object kv2 = args.SelectFieldValueObject(f.Name).OldValue;
                            if (((kv == null) || (kv2 == null)) || !((kv.ToString() == kv2.ToString())))
                            {
                                matched = false;
                                break;
                            }
                        }
                    if (matched)
                    {
                        targetRow = r;
                        break;
                    }
                }
                if (targetRow == null)
                	return 0;
                if (args.CommandName == "Delete")
                	t.Rows.Remove(targetRow);
                else
                	foreach (FieldValue v in args.Values)
                    	if (v.Modified)
                        	if (v.NewValue == null)
                            	targetRow[v.Name] = DBNull.Value;
                            else
                            	targetRow[v.Name] = v.NewValue;
                return 1;
            }
        }
        
        public static int ExecuteNonQuery(DbCommand command)
        {
            int rowsAffected = command.ExecuteNonQuery();
            foreach (DbParameter p in command.Parameters)
            	if (p.Direction == ParameterDirection.ReturnValue)
                {
                    int.TryParse(Convert.ToString(p.Value), out rowsAffected);
                    break;
                }
            if (rowsAffected == -1)
            	rowsAffected = 1;
            return rowsAffected;
        }
    }
    
    public class SinglePhaseTransactionScope : IDisposable
    {
        
        private SortedDictionary<string, DbConnection> _connections;
        
        private DbTransaction _transaction;
        
        private bool _isRoot;
        
        public SinglePhaseTransactionScope()
        {
            _connections = new SortedDictionary<string, DbConnection>();
            if (Current == null)
            {
                _isRoot = true;
                HttpContext.Current.Items["SinglePhaseTransactionScope_Current"] = this;
            }
        }
        
        public static SinglePhaseTransactionScope Current
        {
            get
            {
                if (HttpContext.Current == null)
                	return null;
                return ((SinglePhaseTransactionScope)(HttpContext.Current.Items["SinglePhaseTransactionScope_Current"]));
            }
        }
        
        public SortedDictionary<string, DbConnection> Connections
        {
            get
            {
                return _connections;
            }
        }
        
        public DbTransaction Transaction
        {
            get
            {
                return _transaction;
            }
        }
        
        public bool IsRoot
        {
            get
            {
                return _isRoot;
            }
        }
        
        public void Enlist(DbCommand command)
        {
            DbConnection connection = null;
            if (!(Current.Connections.TryGetValue(command.Connection.ConnectionString, out connection)))
            {
                connection = command.Connection;
                _transaction = connection.BeginTransaction();
                command.Transaction = _transaction;
                Current.Connections.Add(command.Connection.ConnectionString, connection);
            }
            else
            {
                command.Connection = connection;
                command.Transaction = Current.Transaction;
            }
        }
        
        public void Enlist(DbConnection connection)
        {
            if (!(Current.Connections.ContainsKey(connection.ConnectionString)))
            {
                _transaction = connection.BeginTransaction();
                Current.Connections.Add(connection.ConnectionString, connection);
            }
        }
        
        public void Complete()
        {
            if (_isRoot && (_transaction != null))
            	_transaction.Commit();
        }
        
        public void Rollback()
        {
            if (_isRoot && (_transaction != null))
            	_transaction.Rollback();
        }
        
        void IDisposable.Dispose()
        {
            _connections.Clear();
            if (_isRoot)
            	HttpContext.Current.Items["SinglePhaseTransactionScope_Current"] = null;
        }
    }
}
