using System;
using System.Data;
using System.Collections.Generic;
using System.Data.SqlClient;

/// <summary>
/// SqlParameter list
/// </summary>
public class ParamList
{
    private List<SqlParameter> _prams = new List<SqlParameter>();


    /// <summary>
    /// Add input SqlParameter 
    /// </summary>
    /// <param name="paramName">Name of param.</param>
    /// <param name="dbType">Param type.</param>
    /// <param name="size">Param size. use 0 for unknown size.</param>
    /// <param name="value">Param value.</param>
    public void Add(string paramName, SqlDbType dbType, int size, object value)
    {
        _prams.Add(MakeInParam(paramName, dbType, size, value));
    }

    public void Add(SqlParameter p)
    {
        _prams.Add(p);
    }


    /// <summary>
    /// Make Output SqlParameter.
    /// </summary>
    /// <param name="paramName">Name of param.</param>
    /// <param name="dbType">Param type.</param>
    /// <param name="size">Param size.</param>
    public void AddOut(string paramName, SqlDbType dbType, int size)
    {
        _prams.Add(MakeOutParam(paramName, dbType, size));
    }

    /// <summary>
    /// Convert the list to SqlParameter array
    /// </summary>
    /// <returns>SqlParameter[] fo the list</returns>
    public SqlParameter[] ToArray()
    {
        return _prams.ToArray();
    }

    /// <summary>
    /// Make input param.
    /// </summary>
    /// <param name="paramName">Name of param.</param>
    /// <param name="dbType">Param type.</param>
    /// <param name="size">Param size.</param>
    /// <param name="value">Param value.</param>
    /// <returns>New parameter.</returns>
    public SqlParameter MakeInParam(string paramName, SqlDbType dbType, int size, object value)
    {
        return MakeParam(paramName, dbType, size, ParameterDirection.Input, value);
    }

    /// <summary>
    /// Make Output param.
    /// </summary>
    /// <param name="paramName">Name of param.</param>
    /// <param name="dbType">Param type.</param>
    /// <param name="size">Param size.</param>
    /// <returns>New parameter.</returns>
    public SqlParameter MakeOutParam(string paramName, SqlDbType dbType, int size)
    {
        return MakeParam(paramName, dbType, size, ParameterDirection.Output, null);
    }

    /// <summary>
    /// Make stored procedure param.
    /// </summary>
    /// <param name="paramName">Name of param.</param>
    /// <param name="dbType">Param type.</param>
    /// <param name="size">Param size.</param>
    /// <param name="direction">Parm direction.</param>
    /// <param name="value">Param value.</param>
    /// <returns>New parameter.</returns>
    public SqlParameter MakeParam(string paramName, SqlDbType dbType, Int32 size, ParameterDirection direction, object value)
    {
        SqlParameter param;

        if (size > 0)
            param = new SqlParameter(paramName, dbType, size);
        else
            param = new SqlParameter(paramName, dbType);

        param.Direction = direction;
        if ((direction != ParameterDirection.Output))
            param.Value = value;

        return param;
    }
}
