using System;
using System.IO;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Web;
using System.Xml;
using System.Runtime.Serialization;

[Serializable]
public class MyDictionary : Dictionary<string, string>
{
    public MyDictionary()
        : base()
    {
    }

    public MyDictionary(string serialized_data)
        : base()
    {
    }

    public MyDictionary(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public MyDictionary Clone()
    {
        MyDictionary dict = new MyDictionary();
        foreach (KeyValuePair<string, string> kv in this)
        {
            dict.Add(kv.Key, kv.Value);
        }

        return dict;
    }

    new public string this[string key]
    {
        get
        {
            string result = "";
            if (base.ContainsKey(key.ToLower()))
                result = base[key.ToLower()];
            return result;
        }
        set
        {
            if (key != null)
                base[key.ToLower()] = (value == null) ? "" : value;
        }
    }

    new public void Add(string key, string val)
    {
        this[key] = val;
    }

    public void Append(MyDictionary dict)
    {
        foreach (KeyValuePair<string, string> kv in dict)
        {
            if (!this.ContainsKey(kv.Key))
                this.Add(kv.Key, kv.Value);
        }
    }

    new public bool ContainsKey(string key)
    {
        return base.ContainsKey(key.ToLower());
    }


}
