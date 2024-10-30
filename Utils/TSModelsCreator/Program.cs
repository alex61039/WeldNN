using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSModelsCreator
{
    class Program
    {
        static void Main(string[] args)
        {
            var generator = new TSModelGenerator(ConfigurationManager.AppSettings["folder_destination"], ConfigurationManager.AppSettings["filename_destination"]);
            generator.Generate();
        }
    }
}
