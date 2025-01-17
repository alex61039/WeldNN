﻿using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSModelsCreator
{
    public class TSModelGenerator
    {
        string OutputFilename { get; set; }

        public TSModelGenerator(string pathDestination, string filenameDestination)
        {
            OutputFilename = Path.Combine(pathDestination, filenameDestination);
        }

        public void Generate()
        {
            // Fetch all Entity types
            var entityTypes = FetchEtityTypes();

            using (var textWriter = File.CreateText(OutputFilename))
            {
                textWriter.WriteLine("//------------------------------------------------------------------------------");
                textWriter.WriteLine("// <auto-generated>");
                textWriter.WriteLine("//    This code is generated by TSModelGenerator");
                textWriter.WriteLine("//    ");
                textWriter.WriteLine("//    NOTE: it it contains '___' (3 undescores) - fix it in TSModelGenerator!");
                textWriter.WriteLine("// </auto-generated>");
                textWriter.WriteLine("//------------------------------------------------------------------------------");
                textWriter.WriteLine();
                textWriter.WriteLine();

                // Create model for entity
                foreach (var entityType in entityTypes)
                {
                    OutputTable(textWriter, entityType);
                }
            }
        }

        public void OutputTable(StreamWriter writer, EntityType type)
        {
            writer.WriteLine($"export class {type.Name} {{");

            // Properties
            foreach(var prop in type.Properties)
            {
                OutputProperty(writer, prop);
            }
            writer.WriteLine();

            // Navigation properties
            var nav_props = type.NavigationProperties.Where(np => np.DeclaringType == type && np.ToEndMember.RelationshipMultiplicity == RelationshipMultiplicity.Many);
            foreach (var nav_prop in nav_props)
            {
                OutputNavigationProperty(writer, nav_prop);
            }

            writer.WriteLine($"}}");
            writer.WriteLine();
            writer.WriteLine();
        }


        public void OutputNavigationProperty(StreamWriter writer, NavigationProperty prop)
        {
            var endEntity = prop.ToEndMember.GetEntityType();

            // public Users: User[];
            var str = string.Format(
                "       public {0}: {1} | undefined;",
                prop.Name,
                prop.ToEndMember.RelationshipMultiplicity == RelationshipMultiplicity.Many ? (endEntity.Name + "[]") : ("___" + endEntity.Name)
            );

            writer.WriteLine(str);
        }


        public void OutputProperty(StreamWriter writer, System.Data.Entity.Core.Metadata.Edm.EdmProperty prop)
        {
            // Build Typescript type, and default value
            string tsType = "any";
            string tsDefaultvalue = "null";

            switch (prop.TypeName.ToLower())
            {
                case "int":
                case "int32":
                case "int64":
                case "float":
                case "double":
                case "money":
                    tsType = "number";
                    tsDefaultvalue = "0";
                    break;

                case "string":
                case "varchar":
                case "nvarchar":
                case "nvarchar(max)":
                case "time":
                case "timespan":
                    tsType = "string";
                    tsDefaultvalue = "\"\"";
                    break;

                case "date":
                case "datetimeoffset":
                case "datetime":
                    tsType = "Date | undefined";
                    tsDefaultvalue = "";
                    break;

                case "bit":
                case "boolean":
                    tsType = "boolean";
                    tsDefaultvalue = "false";
                    break;

                case "guid":
                case "uniqueidentifier":
                    tsType = "string";
                    tsDefaultvalue = "";
                    break;

                // ========================================================
                case "byte[]":
                case "varbinary":
                case "varbinary(max)":
                    tsType = "any";
                    tsDefaultvalue = "null";
                    break;

                // ========================================================
                default:
                    tsType = $"___{prop.TypeName}";
                    tsDefaultvalue = "null";
                    break;
            }

            // Output
            if (String.IsNullOrEmpty(tsDefaultvalue))
                writer.WriteLine($"     public {prop.Name}: {tsType};");
            else
                writer.WriteLine($"     public {prop.Name}: {tsType} = {tsDefaultvalue};");
        }




        public IEnumerable<EntityType> FetchEtityTypes()
        {


            using (var dbcontext = new DataLayer.Welding.WeldingContext())
            {
                ObjectContext objContext = ((IObjectContextAdapter)dbcontext).ObjectContext;
                MetadataWorkspace workspace = objContext.MetadataWorkspace;
                IEnumerable<EntityType> tables = workspace.GetItems<EntityType>(DataSpace.OSpace).OfType<EntityType>();

                return tables;
            }
        }


    }
}
