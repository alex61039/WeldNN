using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;

public partial class DataLayer : DataLayerBase
{
    public DataLayer(string ConnectionString)
    {
        data = new Database(ConnectionString);
    }

    public DataLayer Clone()
    {
        return new DataLayer(data.ConnectionString);
    }
}
