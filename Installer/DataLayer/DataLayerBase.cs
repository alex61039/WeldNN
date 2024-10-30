using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

/// <summary>
/// abstract class for database manipulations
/// </summary>
public abstract class DataLayerBase : IDisposable
{
    protected Database data;

    /// <summary>
    /// Get XmlReader from db by text query
    /// </summary>
    /// <param name="Query">Text Query</param>
    /// <param name="prams">SqlParameter list</param>
    /// <returns>XmlReader with the results</returns>
    public XmlReader GetXmlReader(string Query, ParamList prams)
    {
        XmlReader xmlReader = null;

        // run the stored procedure
        data.RunQuery(Query, prams, out xmlReader);
        return xmlReader;
    }

    public XmlReader GetXmlReader(string Query)
    {
        if (Query.IndexOf("for xml", StringComparison.CurrentCultureIgnoreCase) == -1)
            Query += " FOR XML AUTO";
        return GetXmlReader(Query, new ParamList());
    }

    public SqlDataReader GetSqlDataReader(string Query)
    {
        return GetSqlDataReader(Query, new ParamList());
    }

    public SqlDataReader GetSqlDataReader(string Query, ParamList prams)
    {
        SqlDataReader reader;
        data.RunQuery(Query, prams, out reader);
        return reader;
    }

    public SqlDataReader GetSqlDataReaderProc(string ProcName)
    {
        return GetSqlDataReaderProc(ProcName, new ParamList());
    }

    public SqlDataReader GetSqlDataReaderProc(string ProcName, ParamList prams)
    {
        SqlDataReader reader;
        data.RunProc(ProcName, prams, out reader);
        // data.RunQuery(Query, prams, out reader);
        return reader;
    }

    public DataTable GetSqlDataTable(string query)
    {
        return data.GetDataSet(query).Tables[0];
    }

    public DataTable GetSqlDataTableProc(string ProcName)
    {
        return GetDataSet(ProcName).Tables[0];
    }

    public DataTable GetSqlDataTableProc(string ProcName, ParamList prams)
    {
        return GetDataSet(ProcName, prams).Tables[0];
    }

    public DataSet GetDataSet(string ProcName)
    {
        return data.GetDataSet(ProcName, new ParamList());
    }

    public DataSet GetDataSet(string ProcName, ParamList prams)
    {
        return data.GetDataSet(ProcName, prams);
    }

    public string GetQueryScalar(string Query)
    {
        string result = "";
        try
        {
            result = data.GetQueryScalar(Query, new ParamList()).ToString();
        }
        catch { }
        return result;
    }

    public void RunQuery(string Query)
    {
        try
        {
            data.GetQueryScalar(Query, new ParamList());
        }
        catch (Exception ex)
        {
            throw new Exception(Query, ex);
        }
    }

    public int RunProc(string ProcName, ParamList prams)
    {
        return data.RunProc(ProcName, prams);
    }

    public void Update(string TableName, ParamList Params, string condition)
    {
        SqlParameter[] arr = Params.ToArray();
        if (arr.Length < 1)
            return;

        string sql = "UPDATE " + TableName + " SET ";
        for (int i = 0; i < arr.Length; i++)
        {
            SqlParameter p = arr[i];
            sql += p.ParameterName + "=";

            if (p.Value == null)
            {
                sql += "NULL,";
            }
            else
            {
                if ((p.SqlDbType == SqlDbType.Time) || (p.SqlDbType == SqlDbType.DateTime) || (p.SqlDbType == SqlDbType.SmallDateTime) || (p.SqlDbType == SqlDbType.DateTimeOffset) || (p.SqlDbType == SqlDbType.Date)
                    || (p.SqlDbType == SqlDbType.VarChar) || (p.SqlDbType == SqlDbType.NVarChar)
                    || (p.SqlDbType == SqlDbType.Text) || (p.SqlDbType == SqlDbType.NText)
                    || (p.SqlDbType == SqlDbType.Char) || (p.SqlDbType == SqlDbType.UniqueIdentifier))
                {
                    if (((p.SqlDbType == SqlDbType.DateTime) || (p.SqlDbType == SqlDbType.Date) || (p.SqlDbType == SqlDbType.SmallDateTime) || (p.SqlDbType == SqlDbType.Time)
                        || (p.SqlDbType == SqlDbType.UniqueIdentifier) || (p.SqlDbType == SqlDbType.DateTimeOffset))
                        && (p.Value.ToString().Trim() == ""))
                        sql += "NULL,";
                    else
                    {
                        // title=N'unicode value'
                        if ((p.SqlDbType == SqlDbType.NVarChar) || (p.SqlDbType == SqlDbType.NText))
                            sql += "N";

                        var val = p.Value;
                        if (val.GetType() == typeof(DateTime) || val.GetType() == typeof(DateTimeOffset))
                            sql += "'" + Escape(((DateTime)val).ToString("yyyy-MM-dd HH:mm:ss")) + "',";
                        else
                            sql += "'" + Escape(val) + "',";
                    }
                }
                else if (p.SqlDbType == SqlDbType.Bit)
                {
                    int b = 0;
                    string v = p.Value.ToString();
                    try
                    {
                        b = v == "1" ? 1
                            : v == "0" ? 0
                            : v == "" ? 0
                            : (Boolean.Parse(v) ? 1 : 0);
                    }
                    catch { }
                    sql += String.Format("{0},", b);
                }
                else
                {
                    if ((p.Value == null) || (String.IsNullOrEmpty(p.Value.ToString())))
                        sql += "NULL,";
                    else
                        sql += "'" + Escape(p.Value) + "',";
                }
            }
        }
        sql = sql.Substring(0, sql.Length - 1);
        sql += " WHERE " + condition;
        try
        {
            RunQuery(sql);
        }
        catch (Exception ex)
        {
            Exception myEx = new Exception(ex.Message + " (" + sql + ")", ex.InnerException);
            throw (myEx);
        }
    }

    public int Insert(string TableName, ParamList Params)
    {
        return Insert(TableName, Params, true);
    }
    public int Insert(string TableName, ParamList Params, bool bIdentityColumnExists)
    {
        SqlParameter[] arr = Params.ToArray();
        if (arr.Length < 1)
            return -1;

        string fields = "", values = "";
        for (int i = 0; i < arr.Length; i++)
        {
            SqlParameter p = arr[i];
            fields += p.ParameterName + ",";

            if (p.Value == null)
            {
                values += "NULL,";
            }
            else
            {
                if ((p.SqlDbType == SqlDbType.Time) || (p.SqlDbType == SqlDbType.DateTime) || (p.SqlDbType == SqlDbType.DateTimeOffset) || (p.SqlDbType == SqlDbType.NVarChar) ||
                    (p.SqlDbType == SqlDbType.SmallDateTime) || (p.SqlDbType == SqlDbType.Text) ||
                    (p.SqlDbType == SqlDbType.VarChar) || (p.SqlDbType == SqlDbType.NText) || (p.SqlDbType == SqlDbType.Date) || (p.SqlDbType == SqlDbType.Char) || (p.SqlDbType == SqlDbType.UniqueIdentifier))
                {
                    if (((p.SqlDbType == SqlDbType.DateTime) || (p.SqlDbType == SqlDbType.Date) || (p.SqlDbType == SqlDbType.SmallDateTime) || (p.SqlDbType == SqlDbType.Time) || (p.SqlDbType == SqlDbType.UniqueIdentifier))
                        && (p.Value.ToString().Trim() == ""))
                        values += "NULL,";
                    else
                    {
                        // title=N'unicode value'
                        if ((p.SqlDbType == SqlDbType.NVarChar) || (p.SqlDbType == SqlDbType.NText))
                            values += "N";

                        var val = p.Value;
                        if (val.GetType() == typeof(DateTime) || val.GetType() == typeof(DateTimeOffset))
                            values += "'" + Escape(((DateTime)val).ToString("yyyy-MM-dd HH:mm:ss")) + "',";
                        else
                            values += "'" + Escape(p.Value) + "',";

                        // values += "'" + Escape(p.Value) + "',";
                    }
                }
                else if (p.SqlDbType == SqlDbType.Bit)
                {
                    int b = 0;
                    string v = p.Value.ToString();
                    try
                    {
                        b = v == "1" ? 1
                            : v == "0" ? 0
                            : v == "" ? 0
                            : (Boolean.Parse(v) ? 1 : 0);
                    }
                    catch { }
                    values += String.Format("{0},", b);
                }
                else
                {
                    if ((p.Value == null) || (String.IsNullOrEmpty(p.Value.ToString())))
                        values += "NULL,";
                    else
                        values += "'" + Escape(p.Value) + "',";
                }
            }
        }
        fields = fields.Substring(0, fields.Length - 1);
        values = values.Substring(0, values.Length - 1);

        string sql = "INSERT INTO " + TableName + " (" + fields + ") VALUES (" + values + ")";
        if (bIdentityColumnExists)
        {
            sql += ";SELECT @@IDENTITY";
        }

        try
        {
            if (bIdentityColumnExists)
            {
                int result = Convert.ToInt32(GetQueryScalar(sql));
                return result;
            }
            else
            {
                RunQuery(sql);
                return 0;
            }
        }
        catch (Exception ex)
        {
            Exception myEx = new Exception(ex.Message + " (" + sql + ")", ex.InnerException);
            throw myEx;
        }

        return -1;
    }

    public string Escape(object s)
    {
        if (s == null) return "''";
        return s.ToString().Replace("'", "''");
    }

    public SqlParameter MakeParam(string name, SqlDbType type, object value)
    {
        SqlParameter p = new SqlParameter(name, type);
        p.Value = value;
        return p;
    }

    public void Close()
    {
        data.Close();
    }

    public MyDictionary GetRow(string TableName, int id)
    {
        MyDictionary dict = new MyDictionary();

        SqlDataReader dr = GetSqlDataReader("SELECT * FROM " + Escape(TableName) + " WHERE id = " + id.ToString());
        if (dr.Read())
        {
            dict = DataReaderRow2Dictionary(dr);
        }
        dr.Close();

        return (dict.Count == 0) ? null : dict;
    }

    public MyDictionary GetRow(string TableName, string guid)
    {
        MyDictionary dict = new MyDictionary();

        // In case when guid isn't GUID
        SqlDataReader dr = null;
        try
        {
            dr = GetSqlDataReader("SELECT * FROM " + Escape(TableName) + " WHERE guid = '" + Escape(guid) + "'");
            if (dr.Read())
            {
                dict = DataReaderRow2Dictionary(dr);
            }
            // dr.Close();
        }
        catch { }
        finally
        {
            try
            {
                dr.Close();
            }
            catch { }
        };


        return (dict.Count == 0) ? null : dict;
    }

    public MyDictionary GetFastData(string tableName, string nameColumn, bool firstEmpty, string cond)
    {
        return GetFastData(tableName, nameColumn, firstEmpty, cond, nameColumn);
    }

    public MyDictionary GetFastData(string tableName, string nameColumn, bool firstEmpty, string cond,
        string orderBy)
    {
        MyDictionary dict = new MyDictionary();
        if (firstEmpty)
            dict.Add("", "");

        string sql = "SELECT id, " + nameColumn + " AS title_clmn FROM " + tableName + " " +
            ((cond != "") ? ("WHERE " + cond) : "")
            + " ORDER BY " + orderBy;
        SqlDataReader dr = GetSqlDataReader(sql);
        while (dr.Read())
        {
            dict.Add(dr["id"].ToString(), dr["title_clmn"].ToString());
        }
        dr.Close();
        return dict;
    }

    public MyDictionary DataReaderRow2Dictionary(SqlDataReader dr)
    {
        MyDictionary dict = new MyDictionary();

        for (int i = 0; i < dr.FieldCount; i++)
        {
            dict[dr.GetName(i).ToLower()] = dr[i].ToString();
        }

        return dict;
    }

    public MyDictionary DataRow2Dictionary(DataTable dt, int rowIndex)
    {
        MyDictionary dict = new MyDictionary();

        if (rowIndex >= dt.Rows.Count)
            return dict;

        for (int i = 0; i < dt.Columns.Count; i++)
        {
            dict[dt.Columns[i].ColumnName.ToLower()] = dt.Rows[rowIndex][i].ToString();
        }

        return dict;
    }

    public void Dispose()
    {
        Close();
    }

    public SqlConnection Connection
    {
        get
        {
            return data.Connection;
        }
    }

    public SqlTransaction BeginTransaction(string transactionName)
    {
        return Connection.BeginTransaction(transactionName);
    }

    public void CommitTransaction(SqlTransaction transaction)
    {
        transaction.Commit();
    }

    public void RollbackTransaction(SqlTransaction transaction)
    {
        transaction.Rollback();
    }

    public void ExecuteNonQuery(string Query, SqlTransaction transaction)
    {
        try
        {
            data.ExecuteNonQuery(Query, transaction);
        }
        catch (Exception ex)
        {
            // throw new Exception(Query, ex);
            throw ex;
        }
    }


    private ServerConnection srvCon;
    private Server server;

    public void ExecuteBatch_BeginTransaction()
    {
        srvCon = new ServerConnection(new SqlConnection(data.ConnectionString));

        server = new Server(srvCon);

        srvCon.BeginTransaction();
    }
    public void ExecuteBatch_CommitTransaction()
    {
        srvCon.CommitTransaction();
    }
    public void ExecuteBatch_RollbackTransaction()
    {
        srvCon.RollBackTransaction();
    }


    public void ExecuteBatchQuery(string Query)
    {
        try
        {
            server.ConnectionContext.ExecuteNonQuery(Query);
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

}
