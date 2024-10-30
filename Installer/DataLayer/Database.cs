using System;
using System.ComponentModel;
using System.Collections;
using System.Diagnostics;
using System.Data;
using System.Data.SqlClient;
using System.Xml;

/// <summary>
/// ADO.NET data access using the SQL Server Managed Provider.
/// </summary>
public class Database : IDisposable
{
    // connection to data source
    private SqlConnection con;
    private string connectionString;

    public Database(string connString)
    {
        connectionString = connString;
        Open();
    }

    /// <summary>
    /// Run stored procedure.
    /// </summary>
    /// <param name="procName">Name of stored procedure.</param>
    /// <returns>Stored procedure return value.</returns>
    public int RunProc(string ProcName)
    {
        SqlCommand cmd = CreateCommand(ProcName, null);
        cmd.ExecuteNonQuery();
        return (int)cmd.Parameters["ReturnValue"].Value;
    }

    /// <summary>
    /// Run stored procedure.
    /// </summary>
    /// <param name="procName">Name of stored procedure.</param>
    /// <param name="prams">Stored procedure params.</param>
    /// <returns>Stored procedure return value.</returns>
    public int RunProc(string ProcName, ParamList Prams)
    {
        SqlCommand cmd = CreateCommand(ProcName, Prams);
        cmd.ExecuteNonQuery();
        return (int)cmd.Parameters["ReturnValue"].Value;
    }

    /// <summary>
    /// Run stored procedure.
    /// </summary>
    /// <param name="procName">Name of stored procedure.</param>
    /// <param name="dataReader">Return result of procedure.</param>
    public void RunProc(string ProcName, out SqlDataReader Reader)
    {
        SqlCommand cmd = CreateCommand(ProcName, null);
        Reader = cmd.ExecuteReader();
    }

    /// <summary>
    /// Run stored procedure.
    /// </summary>
    /// <param name="procName">Name of stored procedure.</param>
    /// <param name="prams">Stored procedure params.</param>
    /// <param name="dataReader">Return result of procedure.</param>
    public void RunProc(string ProcName, ParamList Prams, out SqlDataReader Reader)
    {
        SqlCommand cmd = CreateCommand(ProcName, Prams);
        Reader = cmd.ExecuteReader();
    }

    /// <summary>
    /// Run stored procedure.
    /// </summary>
    /// <param name="procName">Name of stored procedure.</param>
    /// <param name="dataReader">Return result of procedure.</param>
    public void RunProc(string ProcName, out XmlReader Reader)
    {
        SqlCommand cmd = CreateCommand(ProcName, null);
        Reader = cmd.ExecuteXmlReader();
    }

    /// <summary>
    /// Run stored procedure.
    /// </summary>
    /// <param name="procName">Name of stored procedure.</param>
    /// <param name="prams">Stored procedure params.</param>
    /// <param name="dataReader">Return result of procedure.</param>
    public void RunProc(string ProcName, ParamList Prams, out XmlReader Reader)
    {
        SqlCommand cmd = CreateCommand(ProcName, Prams);
        Reader = cmd.ExecuteXmlReader();
    }

    /// <summary>
    /// Retrive DataView from DataBase with the Sql query.
    /// </summary>
    /// <param name="Sql">Sql query.</param>
    /// <param name="prms">Query Parameters.</param>
    /// <returns>New DataView with the retrived data.</returns>
    public DataView GetDataView(string ProcName, ParamList prms)
    {
        SqlCommand cmd = CreateCommand(ProcName, prms);
        DataSet ds = new DataSet();
        Open();
        cmd.Connection = con;
        try
        {
            SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(cmd);
            sqlDataAdapter.Fill(ds);
        }
        catch (Exception ex)
        {
            throw ex;
            //ErrorHendler er = new ErrorHendler();
            //er.ReportError(ex);
        }
        finally
        {
            Close();
        }
        return ds.Tables[0].DefaultView;
    }

    public DataSet GetDataSet(string Query)
    {
        SqlCommand cmd = CreateTextCommand(Query, null);
        DataSet ds = new DataSet();
        Open();
        cmd.Connection = con;
        try
        {
            SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(cmd);
            sqlDataAdapter.Fill(ds);
        }
        catch (Exception ex)
        {
            throw ex;
            //ErrorHendler er = new ErrorHendler();
            //er.ReportError(ex);
        }
        finally
        {
            Close();
        }
        return ds;
    }

    public DataSet GetDataSet(string ProcName, ParamList prms)
    {
        SqlCommand cmd = CreateCommand(ProcName, prms);
        DataSet ds = new DataSet();
        Open();
        cmd.Connection = con;
        try
        {
            SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(cmd);
            sqlDataAdapter.Fill(ds);
        }
        catch (Exception ex)
        {
            throw ex;
            //ErrorHendler er = new ErrorHendler();
            //er.ReportError(ex);
        }
        finally
        {
            Close();
        }
        return ds;
    }

    /// <summary>
    /// Run Query.
    /// </summary>
    /// <param name="Query">The Query String</param>
    /// <param name="prams">Query params.</param>
    /// <param name="Reader">Return result of Query.</param>
    public void RunQuery(string Query, ParamList Prams, out XmlReader Reader)
    {
        SqlCommand cmd = CreateTextCommand(Query, Prams);
        Reader = cmd.ExecuteXmlReader();
    }


    /// <summary>
    /// Retrive one field from Text Query
    /// </summary>
    /// <param name="Query">Text Query</param>
    /// <param name="Prams">Query params.</param>
    /// <returns>Return result of Query</returns>
    public Object GetQueryScalar(string Query, ParamList Prams)
    {
        SqlCommand cmd = CreateTextCommand(Query, Prams);
        return cmd.ExecuteScalar();
    }

    public Object GetQueryScalar(string Query, ParamList Prams, SqlTransaction transaction)
    {
        SqlCommand cmd = CreateTextCommand(Query, Prams);
        cmd.Transaction = transaction;
        return cmd.ExecuteScalar();
    }

    /// <summary>
    /// Retrive one field from stored procedure.
    /// </summary>
    /// <param name="Query">The Name of stored procedure.</param>
    /// <param name="Prams">Query params.</param>
    /// <returns>Return result of Query</returns>
    public Object GetProcScalar(string ProcName, ParamList Prams)
    {
        SqlCommand cmd = CreateCommand(ProcName, Prams);
        return cmd.ExecuteScalar();
    }

    /// <summary>
    /// Run Query.
    /// </summary>
    /// <param name="Query">The Query String</param>
    /// <param name="prams">Query params.</param>
    /// <param name="Reader">Return result of procedure.</param>
    public void RunQuery(string Query, ParamList Prams, out SqlDataReader Reader)
    {
        SqlCommand cmd = CreateTextCommand(Query, Prams);
        Reader = cmd.ExecuteReader();
    }

    public void ExecuteNonQuery(string Query, SqlTransaction transaction)
    {
        SqlCommand cmd = CreateNonQueryCommand(Query);
        cmd.Transaction = transaction;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Create command object used to call stored procedure.
    /// </summary>
    /// <param name="procName">Name of stored procedure.</param>
    /// <param name="prams">Params to stored procedure.</param>
    /// <returns>Command object.</returns>
    private SqlCommand CreateCommand(string ProcName, ParamList Prams)
    {
        // make sure connection is open
        Open();

        //command = new SqlCommand( sprocName, new SqlConnection( ConfigManager.DALConnectionString ) );
        SqlCommand cmd = new SqlCommand(ProcName, con);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 90;

        // add proc parameters
        if (Prams != null)
        {
            SqlParameter[] prams = Prams.ToArray();
            foreach (SqlParameter parameter in prams)
            {
                if (parameter != null)
                    cmd.Parameters.Add(parameter);
            }
        }

        // return param
        cmd.Parameters.Add(
            new SqlParameter("ReturnValue", SqlDbType.Int, 4,
            ParameterDirection.ReturnValue, false, 0, 0,
            string.Empty, DataRowVersion.Default, null));

        return cmd;
    }

    /// <summary>
    /// Create command object used to run table direct command.
    /// </summary>
    /// <param name="procName">Query string.</param>
    /// <param name="prams">Params to Query.</param>
    /// <returns>Command object.</returns>
    private SqlCommand CreateTextCommand(string Query, ParamList Prams)
    {
        // make sure connection is open
        Open();

        //command = new SqlCommand( sprocName, new SqlConnection( ConfigManager.DALConnectionString ) );
        SqlCommand cmd = new SqlCommand(Query, con);
        cmd.CommandType = CommandType.Text;
        cmd.CommandTimeout = 90;

        // add proc parameters
        if (Prams != null)
        {
            SqlParameter[] prams = Prams.ToArray();
            foreach (SqlParameter parameter in prams)
            {
                if (parameter != null)
                    cmd.Parameters.Add(parameter);
            }
        }

        // return param
        cmd.Parameters.Add(
            new SqlParameter("ReturnValue", SqlDbType.Int, 4,
            ParameterDirection.ReturnValue, false, 0, 0,
            string.Empty, DataRowVersion.Default, null));

        return cmd;
    }


    private SqlCommand CreateNonQueryCommand(string Query)
    {
        // make sure connection is open
        Open();

        //command = new SqlCommand( sprocName, new SqlConnection( ConfigManager.DALConnectionString ) );
        SqlCommand cmd = new SqlCommand(Query, con);
        cmd.CommandType = CommandType.Text;
        cmd.CommandTimeout = 90;

        return cmd;
    }


    /// <summary>
    /// Open the connection.
    /// </summary>
    public void Open()
    {
        // open connection
        if (con == null || con.State == ConnectionState.Closed)
        {
            con = new SqlConnection(connectionString);
            con.Open();
        }
    }

    /// <summary>
    /// Close the connection.
    /// </summary>
    public void Close()
    {
        if (con != null)
            con.Close();
    }

    /// <summary>
    /// Release resources.
    /// </summary>
    public void Dispose()
    {
        // make sure connection is closed
        if (con != null)
        {
            if (con.State == ConnectionState.Open)
                con.Close();
            con.Dispose();
            con = null;
        }
    }

    public SqlConnection Connection
    {
        get
        {
            return con;
        }
    }

    public string ConnectionString
    {
        get
        {
            return connectionString;
        }
    }


}